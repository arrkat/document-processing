# Implementation Plan

This is the working plan. It records the decisions made, the reasoning behind them, and progress. The README summarises the final design; this file shows how it was reached.

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
| 2 | Document analyzer | 1h | ✅ Done |
| 2b | Design refinement: one waiting line (Option C), Finished vs Completed, results only in the blob, size assumptions, controllers + Swagger | ~1h | ✅ Done |
| 3 | Processor pipeline | 1.5h | ✅ Done |
| 4 | Job Service core | 1.5h | ✅ Done |
| 5 | WebSocket link | 1.5h | ✅ Done |
| 6 | End-to-end + load | 1h | ✅ Done |
| 7 | Docs + deliverables | 1h | ✅ Done (Docker not yet run) |

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
Client ─► JobsController ─► JobManager.CreateAsync
            1. validate request           (invalid → 400 ValidationProblem; nothing stored)
            2. write blob jobs/{id}/documents.json
            3. store JobMetadata + PendingState
            4. enqueue jobId on dispatch queue
Client ◄─ 202 Accepted + Location + JobSummary (Pending)

── Job Service background (the ONE waiting line) ───────────────────────────────
JobDispatcher, one job at a time in FIFO order:
   still Pending? no → drop it
   ─► POST /api/processing-jobs { jobId, documentsRef, documentCount }
   202 / 200 (already known) → the Processor has the job → Pending ➜ Processing
                     (if the job was cancelled meanwhile → send the Processor a compensating cancel)
   503 Busy        → same job stays at the head; retry after BusyRetryDelay or when a terminal event frees a slot
   unreachable     → backoff (bounded, ~1 min) → Failed(ProcessorUnavailable)
   400 Rejected    → Failed(ProcessorRejected)

── Processor ───────────────────────────────────────────────────────────────────
ProcessingJobManager: validate (400) → IJobRunner.TryStart (admission, one lock):
   already known? → 200 (checked first, so a retry of a running job is never told Busy)
   free job slot? no → 503 Busy (nothing registered)
   register → start in background → 202
ProcessingWorker.ProcessAsync:
   ProcessingStarted → read documents blob → analyse docs (per-job parallelism + global SemaphoreSlim)
   → throttled ProcessingProgressUpdated → write blob jobs/{id}/results.json → ProcessingFinished { resultsRef } → free the slot

── Events back (WebSocket, Processor is the client) ────────────────────────────
ProcessingEventHandler: JobStateTransitions.Apply inside an atomic state update (every event is logged)
   ProcessingFinished → Completed (results.json is already fully written; it stays the results' only home)
   any terminal event → wake the dispatcher (a slot is free)
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

### Size assumptions

These bound the Processor's memory per job. The validator enforces the document count, the per-document limit (`DocumentLimits.MaxContentLength`) and the per-job total (`DocumentLimits.MaxTotalContentLength`), all as `400`. Kestrel's body limit sits above them: any valid job fits under 30 MB.

| Limit | Value | Roughly |
|---|---|---|
| Documents per job | 1,000 | from the spec |
| Characters per document | 100,000 | ~15k words, ~40 pages |
| Characters per job (total) | 10,000,000 | ~10 MB of text, averaging 10k characters per document |
| HTTP request body | 30 MB (Kestrel default) | 10M Hebrew characters is 20 MB in UTF-8, plus JSON overhead |

- **Why both a per-document and a per-job limit:**
  - Per-document only: 1,000 × 100k ≈ 200 MB of memory for one job, and ~2 GB for 10 jobs.
  - Per-job only: one 10M-character document becomes a single huge unit of CPU work that per-job parallelism can't split.
- **Worst case:**
  - Processor input ≈ 10 jobs × 10M characters × 2 bytes (UTF-16) ≈ 200 MB.
  - Processor results ≈ 2 MB.
  - Job Service memory stays small: documents go to the blob immediately, and only job state stays in memory.
- **Lifting the limits** would need streaming: reading documents from the blob one at a time instead of loading them all at once.

### Where data lives (one home each)

| Data | Home | Notes |
|---|---|---|
| Document contents | `jobs/{id}/documents.json` | The Job Service writes it and doesn't keep it. The Processor loads it only while processing |
| Job metadata + state | Job Service `IJobRepository` (in memory) | Small; needed for Pending/Failed/Cancelled jobs (no results), listing/paging, and the compare-and-swap |
| Results | `jobs/{id}/results.json` | Written once by the Processor. The Job Service reads it on demand for `GET …/{id}` and `GET …/{id}/results`; nothing is copied |
| `JobSummary` / `JobDetails` | not stored | Built per request |
| Processor job registry | Processor memory | The Processor's own view (idempotency, cancellation), not a copy of Job Service state |

During processing, results accumulate in an in-memory array (one slot per document, no locks) and are written to `results.json` once, atomically, at the end. If the Processor crashes mid-job, the partial results are lost and the watchdog handles the job.

In-memory storage is allowed by the spec. Swapping `InMemoryJobRepository` for SQLite or Postgres is one new class behind the same interface, and would give restart survival, transactions and SQL paging.

### REST API

- **Style:** controllers (`[ApiController]`): `JobsController` in the Job Service, `ProcessingJobsController` in the Processor. The Processor-events WebSocket stays a single mapped endpoint (`/ws/processor-events`).
- **Swagger:** always on, at `/swagger` on both services (http://localhost:5100/swagger, http://localhost:5200/swagger). Each operation is documented from XML comments: summary, remarks (limits, idempotency, retry behavior) and every response code with its body type. This is the spec's "Swagger flow to POC" deliverable.

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
| POST | `/api/processing-jobs` | 202 started / 200 already known (idempotent on jobId) / 400 / 503 + Retry-After when all job slots are busy |
| GET | `/api/processing-jobs/{jobId}` | 200 `ProcessingJobStatus` / 404, used for reconciliation |
| POST | `/api/processing-jobs/{jobId}/cancel` | 202 / 404 / 409 |

Cancel is `POST …/cancel` on both services, not `DELETE`, because the job is stopped, not deleted.

### WebSocket events
- **Vocabulary:** events describe the Processor's *processing*, not the job's lifecycle. The Processor's terminal success is **Finished** (`ProcessingStatus.Finished`, `ProcessingFinished` event): its work is done and `results.json` is written. Only the Job Service uses **Completed**, which it sets on receiving that report.
- **Envelope:** `{ type, eventId, jobId, sequence, timestamp, ... }`, serialized as polymorphic JSON with a `type` discriminator.
  - `eventId` is used to recognise duplicates.
  - `sequence` increases per job and is used to detect duplicate or stale events.
- **Types:**
  - `ProcessingStarted(totalDocuments)`
  - `ProcessingProgressUpdated(processed, total)`, throttled
  - `ProcessingFinished(resultsRef)`
  - `ProcessingFailed(errorCode, message)`
  - `ProcessingCancelled(processed)`
- **`ErrorCode`** values:
  - `ProcessorUnavailable`
  - `ProcessorRejected`
  - `ProcessorTimeout`
  - `InputUnavailable`
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
- **`JobStateTransitions.Apply(state, event, receivedAt)`** is a **pure** function (the receive time is an input, not a clock call; all timestamps record when the Job Service learned of a change, so the two machines' clocks never need to agree). It maps untrusted events (duplicate, late, out of order) onto legal transitions and returns either a new state or an ignore reason (`Duplicate` / `InvalidTransition`). It runs inside `IJobRepository.TryUpdateStateAsync`, a compare-and-swap that may retry, so all side effects happen in the caller afterwards.
- **`Job(Metadata, State)`** is the read model. `ToSummary()` flattens it into the flat public DTO.
- **Who decides what.** The Job Service decides only what happens *before* the handover: waiting, cancel while waiting, unreachable, rejected. After the Processor accepts a job (its 202), the Job Service **records what the Processor reports** (the 202, the events, or its status when the watchdog asks). It never decides anything about processing itself, so its processing-phase state is a read model fed by the Processor, not a second source of truth.
  ```
  Pending ──(dispatcher gets 202, or any processing event)──► Processing ──(ProcessingFinished)──► Completed
     │                                                           ├──(ProcessingFailed / watchdog: Processor lost it)──► Failed
     │                                                           └──(ProcessingCancelled, after a cancel command)──────► Cancelled
     ├──(cancel while waiting)──► Cancelled
     └──(unreachable for the whole window / rejected)──► Failed
  ```
- **No results ingestion step.** The Processor writes `results.json` atomically *before* sending `ProcessingFinished`, so the blob is complete when the event arrives. The job becomes Completed immediately, and `GET` reads the blob on demand.

### Validation
- **Rules for `POST /api/jobs`:**
  - The documents list is present, with 1..1000 entries.
  - Each id is non-empty, unique, and at most 128 characters.
  - Content is non-null and at most 100,000 characters. Empty or whitespace-only content is **valid**.
- **Where it runs:** the validator is a pure interface returning `IReadOnlyDictionary<fieldPath, messages[]>`. It is called by `JobManager` before anything is stored. The controller copies the errors into a `Dictionary` only because `ValidationProblemDetails` requires `IDictionary`. ASP.NET's implicit "non-nullable means required" check is switched off, so our validators are the only source of validation messages; malformed JSON still gets an automatic 400.
- **The Processor re-validates its own requests** (defense in depth): the jobId is a safe key segment, `documentsRef` is a valid blob key, and `documentCount` is in range.

### Concurrency
- **One waiting line, in the Job Service.** The Processor has no intake queue. It accepts a job only when one of its job slots is free (`MaxConcurrentJobs`, default 10), checked without blocking. Otherwise it answers 503 Busy, and the job stays at the head of the Job Service's dispatch line. That line is an **unbounded** `Channel<DispatchRequest>` on purpose: a job is stored before it is queued, so a full queue would strand it. Admission control belongs to rate limiting (documented, not implemented).
- **Two limits inside the Processor:**
  - **Per job:** at most `PerJobParallelism` (default 4) documents of one job in flight, so one large job can't take every slot.
  - **Global:** every document analysis enters one shared `SemaphoreSlim` (default `ProcessorCount`).

  Jobs × per-job parallelism (10 × 4 = 40) can exceed the core count, and the global limit caps the CPU work actually running at once. .NET doesn't pin work to cores: everything runs on the shared thread pool.
- The analysis is CPU-bound, so `IDocumentAnalyzer.Analyze` is **synchronous** on purpose: there is nothing to wait for, and an async wrapper would only hide where the work runs. Everything that waits (blob reads and writes, the global semaphore, WebSocket sends) is async. A cancel takes effect between documents, which is fine at ≤100k characters (~1 ms per document).
- Nothing blocks: no `.Result`, `.Wait()` or `Thread.Sleep`.
- Tests use the real `Channel`s (in-memory and deterministic) and a slow fake `IDocumentAnalyzer` to measure peak concurrency. No wrapper classes are needed.

### Reliability
- **Don't declare Failed early.** Transient problems are retried for a bounded time. `Failed` means waiting won't help.
- **Processor busy (503)** is not a failure and not an attempt. The dispatcher waits `BusyRetryDelay`, or less if a terminal event frees a slot.
- **Processor unreachable.** POST /api/jobs never calls the Processor directly. The dispatcher retries with exponential backoff for a bounded window (`MaxAttempts`, about 1 minute by default), then marks the job `Failed(ProcessorUnavailable)`.
- **A timeout is not "down".** A lost or slow reply looks identical to an outage, but the Processor may have started the job. That's resolved by idempotency:
  - The **jobId is the idempotency key**. The Processor checks "already known" *before* checking capacity, so a retry of a running job gets 200, not 503.
  - Registration is atomic (`TryAdd`). Finished jobs are kept, so a late retry never reruns a job.
  - Before every retry the dispatcher checks that the job is still Pending. If `ProcessingStarted` arrived over the WebSocket, it stops.
  - Delivery is at-least-once (dispatcher retries) and processing is effectively once (the Processor deduplicates).
- **Duplicate or out-of-order events** are ignored by `Apply`, using the per-job sequence. The first terminal state wins.
- **WebSocket disconnect:**
  - Events go through a bounded `Channel<JobEvent>` with a single reader, the socket client.
  - While disconnected, the client keeps reading and **discards** events, so the channel never fills. It reconnects with backoff and jitter.
  - On every (re)connect it sends a **snapshot**: one event per job, built from the registry's current status. The registry is updated before events are published, so the snapshot supersedes anything discarded.
  - Receivers are idempotent.
- **Handover.** The Processor's 202 means it registered the job and started it, so the dispatcher moves the job to Processing right away. A crash before `ProcessingStarted` can't leave the job stuck in Pending; the watchdog sees it as a stale Processing job.
- **Lost events or a Processor crash.** The watchdog checks Processing jobs that have had no activity for too long:
  - The results blob exists → Completed.
  - The Processor reports Finished → Completed; Failed or Cancelled → apply that; still Processing → leave it.
  - The Processor doesn't know the job (it restarted) → `Failed(ProcessorTimeout)`.
  - The Processor is unreachable → the job stays Processing (the last known truth), a warning is logged, and it's checked again on the next scan.
  - The watchdog never guesses. After the handover only the Processor knows the truth, and "unreachable" may be a network split with the Processor still working. Clients see the silence through `JobSummary.LastUpdatedAt` (e.g. "Processing, last update 8 minutes ago") and can cancel.
- **Cancellation never lies.** The state shows Cancelled only once nothing is running:
  - Pending → Cancelled locally (the Processor never had it) → 202.
  - Processing → a **command** to the Processor; the state stays Processing until `ProcessingCancelled` arrives. Processor 202 → 202 "cancellation requested" · 409 → 409 · 404 (it lost the job) → `Failed(ProcessorTimeout)` + 409 · unreachable → 503, retry.
  - **Cancel racing an in-flight dispatch:** the job is Cancelled locally while the dispatcher's POST is on its way. When the 202 arrives, "Pending → Processing" fails because the job is Cancelled, so the dispatcher sends the Processor a compensating cancel.
  - A 202 for a Processing job means "requested": if processing finished first, the job ends Completed.
- **Shutdown.** The Processor cancels in-flight jobs without sending a terminal event. The watchdog then detects them as `ProcessorTimeout`.

### Text analysis rules
- **Words:**
  1. Split the text on whitespace only (spaces, tabs, newlines).
  2. Remove every character that is not a Unicode letter or digit. Punctuation never splits a word: `don't` → `dont`, `e-mail` → `email`.
  3. Lower-case the result (invariant culture).
  4. Keep it only if it contains at least one letter. Pure numbers are not words (`3.14` and `66` are dropped), while mixed words stay whole (`mp3`, `covid-19` → `covid19`).
  
  Hebrew letters count as letters. Vowel marks (niqqud) are stripped, so the word stays intact.
- **Top-5:** by count descending, then by word ascending (ordinal), so ties are deterministic.
- **SHA-256:** over the UTF-8 bytes of the original content, exactly as submitted, as lowercase hex. It is independent of tokenizing: punctuation, casing, whitespace, line endings and Unicode encoding all affect the hash. Nothing is normalized.
- **Metrics:** exactly the four the spec lists (word count, unique word count, top-5 words, SHA-256). No extra metadata. The analyzer is pure (no clock), so results are deterministic.

### When a job is Completed or Failed
- **No per-document failures.** Validation guarantees every document is analysable (tokenizing, counting and hashing always produce an answer), so `DocumentResult` has no status and jobs have no succeeded/failed counts. An empty document has zero counts, no top words and the hash of `""`.
- **Completed:** every document was analysed.
- **Failed:** reserved for job-level problems where waiting won't help:
  - unreachable after the retry window
  - rejected
  - Processor lost the job
  - input or results unavailable
  - an unexpected error.

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
| Results ingestion / verification step | The Processor writes results atomically before reporting Finished, so re-verifying added a background service, a queue and an error code for nothing. |
| Marking a Processing job Cancelled immediately | It would show Cancelled while the Processor is still working. Cancel is a command; the state follows the Processor's confirmation. |
| Job Service forwarding status reads to the Processor | It would make the WebSocket pointless, fail reads while the Processor is down, and lose history on every Processor restart. The Job Service owns the pre-handover phase and records the rest. |
| Watchdog giving up on a job whose Processor is unreachable | "Unreachable" may be a network split with the Processor still working, so `Failed` would be a guess shown as fact. A bounded wait is right *before* the handover (dispatcher), not after it. The silence is made visible (`LastUpdatedAt`) instead. |
| Waiting for `ProcessingStarted` to leave Pending | A crash between the 202 and the event would leave the job Pending forever. The 202 itself means "started". |
| Per-document status / `CompletedWithErrors` | Validated content can always be analysed, so a per-document failure cannot happen. An unexpected exception is a bug and becomes job-level `Failed(Unexpected)`. |
| Mutable `JobState` class with a lock | Live-list enumeration bugs and unguarded transitions. Replaced by an immutable compare-and-swap design. |
| `/api/process`, `DELETE` for cancel | REST paths are nouns; a cancelled job is not deleted. |
| Idempotency-Key on job creation | Too much for the time-box. Listed as a README limitation. |
| Rate limiting | Same. The Processor's job slots and 503 Busy bound the processing work; jobs wait in the Job Service. |
| Job priority | Not asked for; fairness matters more. A README note covers it. |
| Intake queue inside the Processor | It made a second waiting line for the same jobs. The Processor now accepts only when a slot is free (503 otherwise), and the Job Service holds the one line. |
| Job Service counts Processor capacity | The two services would have to agree on a number, and one lost event makes it drift. Asking the Processor ("503 Busy") is always accurate. |
| Endless dispatch retries | Not production practice. A bounded window, then `Failed`, with a retry endpoint as the documented next step. |
| Wrapper classes around `Channel` / `SemaphoreSlim`, an event factory | Real channels and semaphores are deterministic in tests, and `ChannelWriter` / `ChannelReader` already separate the two sides. |
| Copying results into the Job Service repository | It duplicated `results.json` and made memory grow with every completed job. Results are read from the blob on demand. |
| Drop-oldest-progress outbound buffer | The reconnect snapshot makes discarding events while disconnected safe, which is simpler. |

---

## Documented, not implemented

Production hardening goes in the README's "Known limitations" section instead of the code.

| Item | Rationale |
|---|---|
| `POST /api/jobs/{id}/retry` with retryable error codes (`ProcessorUnavailable`, `ProcessorTimeout`) and `Failed → Pending` | Manual replay instead of endless retries, like a dead-letter queue |
| Processor's "already known" 200 returning the job's current status | Lets a retried dispatch learn the job already completed. Only needed with the retry endpoint |
| Automatic requeue after a Processor restart, with a poison-job cap | Today the watchdog fails the job; retry would be the recovery path |
| Run id per Processor start | After a restart, the same job's sequence numbers restart, so progress can appear to jump backwards. Harmless |
| Double failure: HTTP replies *and* the WebSocket down for the whole retry window | The job is marked Failed although it completed; "first terminal state wins" keeps it Failed |
| Idempotency-Key on `POST /api/jobs` | Duplicate jobs on double clicks or client retries |
| Rate limiting (429) | Today any number of jobs can be submitted; they wait in the dispatch line (memory). The Processor's job slots still bound the processing work |
| Graceful shutdown with requeue | Today the watchdog catches abandoned jobs |
| Job priority | Not requested |
| Retention / eviction of finished jobs and blobs | Job state and blobs grow with job count |
| Documents uploaded by reference: the client puts them in blob storage (pre-signed URL) and `POST /api/jobs` carries refs | The same claim-check as the rest of the system; removes the body limit, keeps contents out of the Job Service, and enables streaming |
| Streaming documents (lifting the size limits) | Today a job's documents are loaded at once; see Size assumptions |
| A body over 30 MB returns a generic 500 | Kestrel's `BadHttpRequestException` (413) is turned into 500 by the .NET 8 exception handler; no valid job reaches 30 MB, so it was left as is |
| Incremental result writes (resume after a crash) | Today results are written once at the end |
| Durable storage, a broker, multiple Processors | In-memory is allowed by the spec; each is "swap the implementation" |
| Pending visibility (attempt count, last dispatch error on the job) | Logged today |
| Letters outside the BMP dropped by the tokenizer | Rare scripts only |
| Processor unreachable after the handover | Jobs stay Processing (the last known truth) until it returns; `LastUpdatedAt` shows how stale the status is, and the watchdog logs a warning. The documented retry endpoint would be the operator's tool |
| Progress is eventually consistent | Progress events are throttled and droppable, so GET may lag slightly; final states are never dropped |
| A Job Service restart loses all jobs | In-memory storage (allowed by the spec); the Processor's later events for unknown jobs are logged and ignored. SQLite is the documented swap |
| An unreadable results blob | GET returns an error for that request. Only possible through outside interference, since the Processor writes atomically before reporting |
| Cancellation inside a single document's analysis | Needed only for very large documents; today a cancel applies between documents |

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
2. **Document analyzer** ✅ `TextMetrics` and `DocumentAnalyzer.Analyze`, with fixed sample documents and known answers:
   - empty and whitespace-only content
   - punctuation and casing
   - Hebrew/Unicode
   - ties in the top-5
   - fewer than 5 unique words
   - a large document
   - a hash checked with `shasum -a 256`.
3. **Processor pipeline** ✅
   - `ProcessJobRequestValidator`, `ProcessingJobManager`, `JobRegistry`
   - `ProcessingWorker` (job slots, global `SemaphoreSlim`, `TryStart`, `ProcessAsync`)
   - Tests:
     - invalid → 400; known jobId → 200 even when busy; all slots busy → 503 with nothing registered
     - registry transitions; the first terminal state wins
     - happy path writes the results blob and emits Started → Progress → Completed
     - the global cap is never exceeded (slow fake analyzer)
     - cancelling a running job → ProcessingCancelled
     - missing input blob → `Failed(InputUnavailable)`
     - analyzer exception → `Failed(Unexpected)`.
4. **Job Service core** ✅
   - The state transition methods, `JobStateTransitions.Apply` (table-driven tests) and `Job.ToSummary`
   - `InMemoryJobRepository` (compare-and-swap)
   - `CreateJobRequestValidator`, `JobManager`
   - The dispatch line (a plain `Channel<DispatchRequest>`), `JobDispatcher` (FIFO; Busy is not an attempt; bounded unreachable window; Processing on 202; compensating cancel; wake-up on terminal events), `ProcessorClient` (tested with a fake `HttpMessageHandler`)
   - `JobWatchdog` (blob exists / Processor says Finished → Completed; Processor lost the job → Failed(ProcessorTimeout); unreachable → unchanged + warning)
5. **WebSocket link** ✅
   - Job Service side: `ProcessorEventsReceiver` and `JobEventHandler`.
   - Processor side: `JobEventsSocketClient` with reconnect and snapshot.
   - Tests: duplicate events, out-of-order events, reconnect.
6. **End-to-end + load.** ✅
   - `TwoServiceHost` hosts both services in one process (two `WebApplicationFactory`s, a shared temp blob folder):
     - the Job Service's `ProcessorClient` sends through the Processor's test server handler, resolved on the first request (resolving it at startup would start the Processor inside the Job Service's own startup and hang);
     - the Processor's `IWebSocketConnector` connects to the Job Service's test server.
   - Tests:
     - happy path (English, Hebrew, empty) → Completed, results equal `DocumentAnalyzer` on the same documents, also via `GET …/results`
     - 6 jobs on 2 job slots → some wait as Pending, all Completed
     - cancel mid-document → 202, stays Processing until the Processor confirms, then Cancelled.
   - `LoadGen`: a seeded 5,000-word vocabulary with a Zipf-like distribution (weight 1/rank); all jobs submitted concurrently, then polled; reports throughput and p50/p95/max of server-side job time. Exit code 1 if any job did not complete.
   - **Result** (Release build, Apple M1, 8 cores; `--jobs 10 --docs 1000 --words 200..3000 --seed 42`, 9.8M characters per job, 98M in total):

     | Metric | Value |
     |---|---|
     | Status | 10/10 Completed |
     | Submit (all 10 jobs) | 0.38 s |
     | Wall time (first submit → last finished) | 1.73 s |
     | Throughput | ~5,800 documents/s, ~57M characters/s |
     | Job time, created → completed | p50 1.24 s, p95 1.27 s |
     | Processing, started → completed | p50 1.11 s, p95 1.15 s |

     With 10 jobs, p95 is effectively the max.
7. **Docs + deliverables.** ✅
   - Every README section from the spec, including AI usage with representative prompts and rejected suggestions (results in events, `AwaitingResults`, the results-ingestion step → read the blob on GET, the watchdog timeout, the Processor's intake queue → backpressure, extra metadata).
   - A Postman collection.
   - Optional docker-compose with a shared blob volume.

## Verification
- `dotnet test` passes.
- Run both services, then use Swagger/Postman:
  - Happy path: POST → poll GET → Completed with results.
  - An invalid payload returns 400.
- Stop the Processor, then POST a job → retries for about a minute, then `Failed(ProcessorUnavailable)`. Start the Processor within the window → the job completes.
- Submit more jobs than there are job slots → the extra jobs stay Pending, then start as slots free up. None fail.
- Restart the Processor mid-job → the WebSocket reconnects, and the watchdog marks the job `Failed(ProcessorTimeout)`.
- `dotnet run --project tools/LoadGen -- --jobs 10 --docs 1000` → all jobs reach a terminal state, and the timings go in the README.
