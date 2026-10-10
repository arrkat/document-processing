# Implementation Plan

This is the working plan for the TA9 home assignment. It records the decisions made, the reasoning behind them, and progress. The README summarises the final design; this file shows how it was reached.

## Context

The assignment is two .NET 8 services, with an 8–10h time-box:
- **Job Service** accepts jobs over REST, holds job state, and receives processing events over WebSocket.
- **Processor Service** accepts work over REST, analyses documents concurrently, and reports the job lifecycle back over WebSocket.

The graders care about structure, failure handling, concurrency, tests and documented trade-offs more than about feature count.

---

## Roadmap

| # | Stage | Est. | Status |
|---|---|---|---|
| 1 | Skeleton: projects, contracts, interfaces, REST/WS routes, DI, blob storage | ~1h | ✅ Done |
| 2 | Document analyzer | 1h | ⏭ Next |
| 3 | Processor pipeline | 1.5h | ⬜ |
| 4 | Job Service core | 1.5h | ⬜ |
| 5 | WebSocket link | 1.5h | ⬜ |
| 6 | End-to-end + load | 1h | ⬜ |
| 7 | Docs + deliverables | 1h | ⬜ |
| 8 | Bonus: Audit Service, only if time remains | — | ⬜ |

Stages 3 and 4 are independent of each other. Stage 5 needs both.

**Working method:**
- The skeleton has every interface, DTO, route and DI registration in place.
- Methods with logic throw `NotImplementedException` and are implemented test-first.
- Background services start as no-ops, so both apps boot at every stage.

---

## Architecture

### End-to-end flow

```
── POST /api/jobs (synchronous) ────────────────────────────────────────────────
Client ─► JobsEndpoints ─► JobManager.CreateAsync
            1. validate request           (invalid → 400 ValidationProblem; nothing stored)
            2. write blob jobs/{id}/documents.json
            3. store JobMetadata + PendingState
            4. enqueue jobId on dispatch queue
Client ◄─ 202 Accepted + Location + JobSummary (Pending)

── Job Service background ──────────────────────────────────────────────────────
JobDispatcher ─► POST /api/processing-jobs { jobId, documentsRef, documentCount }
   202/200 → done · 503/unreachable → backoff + retry → Failed(ProcessorUnavailable) · 400 → Failed(ProcessorRejected)

── Processor ───────────────────────────────────────────────────────────────────
register job (+ cancellation token) → bounded intake queue → worker picks it up
   → JobStarted → read documents blob → analyse docs (per-job parallelism + global limiter)
   → throttled JobProgress → write blob jobs/{id}/results.json → JobCompleted { resultsRef }

── Events back (WebSocket, Processor is the client) ────────────────────────────
JobEventHandler: JobStateTransitions.Apply inside an atomic state update
   JobCompleted → job stays Processing; enqueue results ingestion
ResultsIngestor: read results blob → save results → ProcessingState.Complete → Completed
```

### Claim-check: messages carry metadata and references only
- Documents and results travel through `IBlobStore`. REST requests and events carry blob keys plus counts.
  - `FileSystemBlobStore` uses one root folder shared by both services (a named volume in Docker).
  - `InMemoryBlobStore` is used in tests.
  - S3/Azure Blob would be another implementation.
- Writes are atomic: write to a temp file, then rename it into place.
- Keys use a restricted character set, plus a check that the path stays under the root. `documentsRef` arrives over HTTP, so path traversal has to be blocked.
- The results key is deterministic (`BlobKey.JobResults(jobId)`), so the Job Service can always rebuild it.
- Blob payloads carry a `SchemaVersion`.

### REST API

**Job Service**

| Method | Path | Result |
|---|---|---|
| POST | `/api/jobs` | 202 + `JobSummary` + `Location`; 400 ValidationProblem |
| GET | `/api/jobs?status=&page=&pageSize=` | 200 `PagedResult<JobSummary>` |
| GET | `/api/jobs/{jobId}` | 200 `JobDetails` / 404 |
| GET | `/api/jobs/{jobId}/results` | 200 / 404 |
| POST | `/api/jobs/{jobId}/cancel` | 202 / 404 / 409 |
| WS | `/ws/processor-events` | the Processor connects here |

**Processor Service.** The spec's `/api/process` is renamed `/api/processing-jobs`, because REST paths are nouns.

| Method | Path | Result |
|---|---|---|
| POST | `/api/processing-jobs` | 202 new / 200 already known (idempotent on jobId) / 400 / 503 + Retry-After |
| GET | `/api/processing-jobs/{jobId}` | 200 `ProcessingJobStatus` / 404, used for reconciliation |
| POST | `/api/processing-jobs/{jobId}/cancel` | 202 / 404 / 409 |

Cancel is `POST …/cancel` on both services, not `DELETE`, because the job is stopped, not deleted.

### WebSocket events
- **Envelope:** `{ type, eventId, jobId, sequence, timestamp, ... }`, serialized as polymorphic JSON with a `type` discriminator.
  - `eventId` is used to recognise duplicates.
  - `sequence` increases per job and is used to detect duplicate or stale events.
- **Types:**
  - `JobStarted(totalDocuments)`
  - `JobProgress(processed, total)`, throttled
  - `JobCompleted(resultsRef, succeeded, failed)`
  - `JobFailed(errorCode, message)`
  - `JobCancelled(processed)`
- **`ErrorCode`** values:
  - `ProcessorUnavailable`
  - `ProcessorRejected`
  - `ProcessorTimeout`
  - `ProcessorShuttingDown`
  - `InputUnavailable`
  - `ResultsUnavailable`
  - `Unexpected`

### Job Service domain model
- **`JobMetadata`** (immutable, insert-only): Id, TotalDocuments, DocumentsRef, CreatedAt.
- **`JobState`** is polymorphic and immutable. There are 5 states, mapping one-to-one to the public `JobStatus` enum:
  ```
  Pending ──Start──► Processing ──Complete──► Completed
     ├──Fail──► Failed ◄──┤
     └──Cancel─► Cancelled ◄┘
  ```
  - Transition methods exist only on the states where the transition is legal. Terminal states have none.
  - The base record carries `LastActivityAt` (for the watchdog) and `LastEventSequence` (for dedupe).
  - Nullables remain only where they describe reality. For example, `FailedState.StartedAt` is null when the job failed before it started.
- **`JobStateTransitions.Apply(state, event)`** is a **pure** function. It maps untrusted events (duplicate, late, out of order) onto legal transitions and returns either a new state or an ignore reason (`Duplicate` / `InvalidTransition`). It runs inside `IJobRepository.TryUpdateStateAsync`, a compare-and-swap that may retry, so all side effects happen in the caller afterwards.
- **`Job(Metadata, State)`** is the read model. `ToSummary()` flattens it into the flat public DTO.
- **Results ingestion is internal Job Service work, not a lifecycle state.**
  - `JobCompleted` leaves the job in Processing and only bumps its sequence.
  - The ingestor calls `Complete` after the results have been saved.
  - Ingestion is idempotent, because only one `Complete` can win.

### Validation
- **Rules for `POST /api/jobs`:**
  - The documents list is present, with 1..1000 entries.
  - Each id is non-empty, unique, and at most 128 characters.
  - Content is non-null and at most 1M characters. Empty or whitespace-only content is **valid**.
- **Where it runs:** the validator is a pure interface returning `IReadOnlyDictionary<fieldPath, messages[]>`. It is called by `JobManager` before anything is stored. The endpoint copies the errors into a `Dictionary` only because `TypedResults.ValidationProblem` requires `IDictionary`.
- **The Processor re-validates its own requests** (defense in depth): the jobId is a safe key segment, `documentsRef` is a valid blob key, and `documentCount` is in range.

### Concurrency
- The Processor runs N job workers (default 10) that read from a bounded `Channel<ProcessingJob>`.
- Each job processes its documents with small per-job parallelism.
- Every document acquires a **global** `SemaphoreSlim` (default `ProcessorCount`) before its CPU work.
- Together these give a bounded total and fair interleaving between jobs. `Parallel.ForEachAsync` alone limits only within one job.
- The analysis is CPU-bound, so async helps only for I/O.
- Nothing blocks: no `.Result`, `.Wait()` or `Thread.Sleep`.
- When the queue is full, `TryEnqueue` returns false and the endpoint responds 503 + Retry-After. The dispatcher treats 503 as retryable.

### Reliability
- **Processor unreachable.** POST /api/jobs never calls the Processor directly. The dispatcher retries with backoff, then marks the job `Failed(ProcessorUnavailable)`.
- **Idempotency between the services.** Submitting the same jobId twice returns 200 and does no new work.
- **Duplicate or out-of-order events** are ignored by `Apply`, using the per-job sequence.
- **WebSocket disconnect:**
  - The Processor buffers outbound events in a bounded buffer. When it's full, it drops the oldest progress events but never terminal events.
  - It reconnects with backoff and jitter, resends the event that was in flight when the connection dropped, then resends a snapshot of every known job.
  - Delivery is at-least-once; receivers are idempotent.
- **Lost `JobCompleted` or a Processor crash.** The watchdog checks Processing jobs that have had no activity for too long:
  - The results blob exists → ingest it.
  - The Processor reports Failed or Cancelled → apply that.
  - The Processor doesn't know the job → `Failed(ProcessorTimeout)`.
  - The Processor is unreachable → retry on the next scan.
- **Cancellation.**
  - A cancellation token is registered before the job is enqueued, so a job cancelled while queued is skipped.
  - The first terminal state wins. A cancel that races with ingestion may win, which is documented.
- **Shutdown.** The Processor stops reading new jobs, and in-flight jobs end with `Failed(ProcessorShuttingDown)`.

### Text analysis rules
- **Words:** case-insensitive runs of Unicode letters and digits, with inner apostrophes allowed.
- **Top-5:** by count descending, then by word ascending (ordinal), so ties are deterministic.
- **SHA-256:** over the UTF-8 bytes of the original content, as lowercase hex.
- **Metrics:** exactly the four the spec lists (word count, unique word count, top-5 words, SHA-256). No extra metadata. The analyzer is pure (no clock), so results are deterministic.
- **Errors:** an exception on one document produces a `Failed` `DocumentResult`, and the job continues.

### Job outcome semantics
- **Completed:** every document reached a terminal per-document status. The succeeded/failed counts show partial problems; there is no extra "CompletedWithErrors" status.
- **Failed:** reserved for job-level problems (unreachable, timeout, input or results unavailable, unexpected error).

### Observability
- Structured `ILogger` logging with a JobId scope.
- `/health` on both services.
- Optional: correlation id, WebSocket-connected health check, `System.Diagnostics.Metrics` counters.

---

## Considered and rejected

| Alternative | Why not |
|---|---|
| Results inside WebSocket events | Messages should carry metadata. Claim-check via blobs instead. |
| Pull documents via `GET /api/jobs/{id}/documents` | Creates a circular dependency and a new failure mode. The blob ref replaces it. |
| Shared database between the services | Couples the schemas, makes the WebSocket pointless, and puts two writers on the same status rows. |
| Message broker | The spec lists it as not required and requires REST + WebSocket. Mentioned as the production choice. |
| `AwaitingResults` job state | Results ingestion is internal Job Service work, not part of the lifecycle. |
| `CompletedWithErrors` status | One more state for clients; the counts already carry this information. |
| Mutable `JobState` class with a lock | Live-list enumeration bugs and unguarded transitions. Replaced by an immutable compare-and-swap design. |
| `/api/process`, `DELETE` for cancel | REST paths are nouns; a cancelled job is not deleted. |
| Idempotency-Key on job creation | Too much for the time-box. Listed as a README limitation. |
| Rate limiting | Same. Bounded queues and 503 are the overload story. |
| Job priority | Not asked for; fairness matters more. A README note covers it. |

---

## Stage details

1. **Skeleton** ✅
   - The solution contains:
     - `src/Contracts`
     - `src/BlobStorage`
     - `src/JobService` (:5100)
     - `src/ProcessorService` (:5200)
     - `tests/*`
     - `tools/LoadGen`
   - Blob storage is implemented with contract tests run against both stores.
   - Health checks pass in integration tests.
2. **Document analyzer.** `DocumentAnalyzer.Analyze`, with fixed sample documents and known answers:
   - empty and whitespace-only content
   - punctuation and casing
   - Hebrew/Unicode
   - ties in the top-5
   - fewer than 5 unique words
   - a large document
   - a hash checked with `shasum -a 256`.
3. **Processor pipeline.**
   - `JobRegistry`, `ChannelProcessingQueue`, `SemaphoreDocumentConcurrencyLimiter`
   - `JobEventFactory`, `OutboundEventBuffer`
   - `JobProcessor`, `ProcessingWorkerPool`
   - `ProcessJobRequestValidator`, `ProcessingService`
   - Tests: the concurrency cap is never exceeded, cancelling queued and running jobs, the full queue returns 503, and idempotent resubmits.
4. **Job Service core.**
   - The state transition methods, `JobStateTransitions.Apply` (table-driven tests) and `Job.ToSummary`
   - `InMemoryJobRepository` (compare-and-swap)
   - `CreateJobRequestValidator`, `JobManager`
   - The dispatch and ingestion queues, `JobDispatcher`, `ProcessorClient` (tested with a fake `HttpMessageHandler`)
   - `ResultsIngestor`, `JobWatchdog`
5. **WebSocket link.**
   - Job Service side: `ProcessorEventsSocketHandler` and `JobEventHandler`.
   - Processor side: `JobEventsSocketClient` with reconnect and snapshot.
   - Tests: duplicate events, out-of-order events, reconnect.
6. **End-to-end + load.**
   - An integration test hosts both services in one process (`WebApplicationFactory`, with the WebSocket client wired to the Job Service test server).
   - `LoadGen`: a seeded vocabulary with a Zipf-like distribution, `--jobs 10 --docs 1000`, reporting throughput and p50/p95 job duration.
7. **Docs + deliverables.**
   - Every README section from the spec, including AI usage with representative prompts and a rejected suggestion. Good examples are the `Parallel.ForEachAsync` fairness claim and results-in-events.
   - A Postman collection.
   - Optional docker-compose with a shared blob volume.
8. **Bonus: Audit Service.** The Job Service POSTs terminal job summaries to it. Built only if time remains.

## Verification
- `dotnet test` passes.
- Run both services, then use Swagger/Postman:
  - Happy path: POST → poll GET → Completed with results.
  - An invalid payload returns 400.
- Stop the Processor, then POST a job → retries, then `Failed(ProcessorUnavailable)`.
- Restart the Processor mid-job → the WebSocket reconnects, and the job completes through reconciliation or fails through the watchdog.
- `dotnet run --project tools/LoadGen -- --jobs 10 --docs 1000` → all jobs reach a terminal state, and the timings go in the README.
