# Real-Time Document Processing System

Two .NET 8 services. The **Job Service** accepts jobs and tracks their state. The **Processor Service** analyses the documents concurrently. They talk over REST (Job Service → Processor) and WebSocket (Processor → Job Service). Document and result payloads travel through a shared blob store (claim-check pattern). Requests and events carry only metadata and blob references.

More detail: [docs/HAPPY_FLOW.md](docs/HAPPY_FLOW.md) (one job end to end, as a sequence diagram) and [docs/PLAN.md](docs/PLAN.md) (every decision, the alternatives rejected, and why).

## Build & run

Requires the .NET 8 SDK or newer (`global.json` accepts 8.0.100 and up, including 9 and 10) and the .NET 8 runtime.

```bash
dotnet build
```

Run each service in its own terminal (start order doesn't matter):

```bash
dotnet run --project src/ProcessorService
```

```bash
dotnet run --project src/JobService
```

| Service | URL | Swagger |
|---|---|---|
| Job Service | http://localhost:5100 | http://localhost:5100/swagger |
| Processor Service | http://localhost:5200 | http://localhost:5200/swagger |

Both services share the blob root `data/blobs` at the repo root, set by `BlobStorage:RootPath` in each `appsettings.json`.

**Docker (optional).** Both services and a shared named volume for the blobs, on the same ports:

```bash
docker compose up --build
```

**Trying it out.**
- **Swagger** on each service documents every endpoint, its limits and response codes.
- **Postman:** import [postman/DocumentProcessing.postman_collection.json](postman/DocumentProcessing.postman_collection.json). It has the happy path (create → poll → results), a 1,000-document job to watch or cancel, invalid input and health checks.
- **Load generator:** with both services running, submits 10 jobs × 1,000 documents and reports throughput and job durations:

  ```bash
  dotnet run -c Release --project tools/LoadGen -- --jobs 10 --docs 1000 --words 200..3000 --seed 42
  ```

## Tests

```bash
dotnet test
```

| Project | What it covers |
|---|---|
| `ProcessorService.Tests` | Text analysis with known answers (Hebrew, punctuation, ties, empty), request validation, job admission (known job → 200 even when busy, full → 503), per-job and global concurrency caps, cancel, missing input, analyzer crash, WebSocket client (snapshot, discard while disconnected, reconnect) |
| `JobService.Tests` | State transitions (table-driven: duplicates, out-of-order, late events), compare-and-swap repository, validation, dispatcher (FIFO, Busy, bounded retry, compensating cancel), watchdog, `ProcessorClient` against a fake handler, WebSocket receiver |
| `BlobStorage.Tests` | One contract test suite run against both stores, path-traversal protection |
| `Integration.Tests` | Both services in one process, wired REST + WebSocket: happy path, more jobs than job slots, cancel mid-processing |

Code coverage (coverlet + ReportGenerator, restored as a local .NET tool) runs every test project and merges the results into `TestResults/coverage-report/index.html`. Line coverage is about 96%.

```bash
scripts/coverage.sh
```

On Windows: `scripts\coverage.ps1`.

## Architecture overview

```
Client ──REST──► Job Service ──REST (jobId + blob ref)──► Processor Service
                   ▲   │                                        │
                   │   └── writes documents.json ─► Blob store ◄┤ reads documents.json,
                   │       reads results.json ◄─────            │ writes results.json
                   └──────────── WebSocket events ──────────────┘
```

- **Job Service** owns the jobs. `POST /api/jobs` validates the documents, writes them to the blob store, stores the job as `Pending` and puts it in the **dispatch line**, then returns `202` at once. A background dispatcher hands jobs to the Processor one at a time, in order. Processing events arriving over the WebSocket update the job's state.
- **Processor Service** owns the processing. It accepts a job only when one of its job slots is free (else `503 Busy`), analyses the documents concurrently, writes `results.json` once, and reports `Started → Progress → Finished` over the WebSocket.
- **Blob store** (a shared folder) is the only home of document contents and results. REST requests and events carry blob keys and counts, never payloads.

**Job lifecycle** (Job Service):

```
Pending ──► Processing ──► Completed
   │             ├──► Failed
   ├──► Failed   └──► Cancelled
   └──► Cancelled
```

The Job Service decides only what happens **before** the handover (waiting, cancel while waiting, Processor unreachable or rejecting). Once the Processor accepts a job, the Job Service **records** what the Processor reports; it never guesses.

**WebSocket events** (Processor → Job Service), JSON with a `type` discriminator:

| Event | Payload |
|---|---|
| `ProcessingStarted` | `totalDocuments` |
| `ProcessingProgressUpdated` | `processedDocuments`, `totalDocuments` (throttled) |
| `ProcessingFinished` | `resultsRef` (results are already fully written) |
| `ProcessingFailed` | `errorCode`, `errorMessage` |
| `ProcessingCancelled` | `processedDocuments` |

Every event carries `eventId`, `jobId`, a per-job increasing `sequence` and a `timestamp`.

**Per document** the Processor computes word count, unique word count, the five most frequent words (ties broken alphabetically) and the SHA-256 of the original text. Words are split on whitespace, lower-cased, with punctuation removed (`don't` → `dont`); pure numbers are not words. Hebrew works, and vowel marks are ignored.

**Storage.** Job state is in memory behind `IJobRepository`, and blobs are behind `IBlobStore`. Switching to SQLite/Postgres or S3/Azure Blob means one new class per interface.

## Key technical decisions & trade-offs

| Decision | Why | Trade-off |
|---|---|---|
| **Claim-check:** payloads in a blob store, messages carry references | Events and REST calls stay small; results have one home (`results.json`); the Job Service never holds document contents | Both services need the same storage (a shared folder here; S3/Azure Blob in production) |
| **One waiting line, in the Job Service.** The Processor has no queue: it accepts when a slot is free, otherwise `503 Busy` | Only one place where jobs wait; the Processor's answer is always accurate, so the services never have to agree on a capacity number | A busy Processor costs a retry round-trip (`BusyRetryDelay`, cut short when a job finishes) |
| **The job id is the idempotency key** for the handover | A lost reply looks like an outage, but the Processor may have started the job. Retrying is safe: a known job gets `200`, never a second run | The Processor keeps finished jobs in memory |
| **Bounded retries before the handover, none after** | Unreachable for ~1 minute → `Failed(ProcessorUnavailable)`. After the handover only the Processor knows the truth, so the Job Service waits instead of guessing | A Processing job whose Processor is unreachable stays `Processing`; `lastUpdatedAt` shows how stale it is |
| **Cancel is a command for Processing jobs** | The job shows `Cancelled` only when nothing is running any more | `202` means "requested"; if processing finished first, the job ends `Completed` |
| **Immutable job states with compare-and-swap updates**, transitions in a pure function | Events can arrive duplicated, late or out of order; one pure, table-tested function decides what each means | More types than a mutable status field |
| **REST nouns:** `/api/processing-jobs` instead of the spec's `/api/process`; cancel is `POST …/{id}/cancel`, not `DELETE` | Paths name resources; a cancelled job is stopped, not deleted | Differs from the spec's name |
| **In-memory storage** (allowed by the spec) | Simple, fast, no setup | A restart loses jobs; the interfaces make SQLite a one-class change |

Considered and rejected (full list in [PLAN.md](docs/PLAN.md#considered-and-rejected)): results inside WebSocket events, a shared database, a message broker, a queue inside the Processor, endless dispatch retries, and a watchdog that marks silent jobs `Failed` after a timeout.

## Concurrency & performance

**Strategy.** Work is bounded at every level, and nothing blocks a thread:

| Limit | Default | Enforced by |
|---|---|---|
| Jobs processed at once | 10 (`Processing:MaxConcurrentJobs`) | job slots, checked when the Processor accepts a job; more → `503 Busy`, and the job waits in the Job Service's line |
| Documents in flight per job | 4 (`Processing:PerJobParallelism`) | `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` |
| Documents analysed at once, all jobs | CPU core count (`Processing:MaxConcurrentDocuments`) | one shared `SemaphoreSlim` |

**Why.** The analysis is CPU-bound, so running more of it at once than there are cores only adds context switching. The global semaphore caps the real CPU work. The per-job cap keeps one 1,000-document job from taking every core while other jobs wait, so jobs progress side by side. `IDocumentAnalyzer.Analyze` is synchronous on purpose (there is nothing to wait for), while everything that does wait (blob I/O, the semaphore, WebSocket sends) is async. No `.Result`, `.Wait()` or `Thread.Sleep`.

**Limits and trade-offs.**
- A job's documents are loaded into memory at once, so their size is capped: at most 1,000 documents, 100,000 characters each and 10M characters per job in total (all three enforced). That bounds the Processor to about 200 MB of document text for 10 concurrent jobs.
- A cancel takes effect between documents (~1 ms each at the size limit), not inside one.
- Waking waiters on the global semaphore in arrival order is how .NET implements `SemaphoreSlim` today, not a documented guarantee. The hard fairness guarantee is the per-job cap.
- The dispatch line is unbounded: there is no rate limiting (see limitations).

**Measured** with `tools/LoadGen` (Release build, Apple M1 with 8 cores; 10 jobs × 1,000 documents, 200–3,000 words each, about 98M characters in total):

| Metric | Value |
|---|---|
| Result | 10/10 Completed |
| Wall time (first submit → last job finished) | 1.73 s |
| Throughput | ~5,800 documents/s, ~57M characters/s |
| Job time, created → completed | p50 1.24 s, p95 1.27 s |
| Processing, started → completed | p50 1.11 s, p95 1.15 s |

All ten jobs finished within ~40 ms of each other, which is what jobs sharing the CPU look like (one after another would spread completions from ~0.1 s to ~1.1 s). With 10 jobs, p95 is effectively the maximum.

## Reliability & edge cases

| Situation | Behavior |
|---|---|
| Invalid input | `400` with every problem by field path (e.g. `documents[1].id`); nothing is stored. Limits: 1–1,000 documents, unique non-empty ids ≤ 128 characters, content ≤ 100,000 characters (empty content is valid), ≤ 10M characters per job in total |
| Request body over 30 MB | Kestrel's body limit rejects it before validation, and the client gets a generic `500` (in .NET 8 the exception handler maps every exception to 500). No valid job comes near it: 10M Hebrew characters is about 20 MB |
| Processor unreachable | The job stays `Pending` while the dispatcher retries with exponential backoff for about a minute, then `Failed(ProcessorUnavailable)`. If the Processor comes back in time, the job runs normally |
| Processor busy | Not a failure: the job waits at the head of the line and is retried when a job finishes |
| Processor rejects the job | `Failed(ProcessorRejected)` |
| Reply lost after the Processor accepted | The retry gets `200` ("already known"), and the job is not run twice |
| Processor crashes or restarts mid-job | The watchdog checks jobs with no activity for 60 s: results written → `Completed`; the Processor no longer knows the job → `Failed(ProcessorTimeout)`; Processor unreachable → stays `Processing` and a warning is logged |
| Documents blob missing | `Failed(InputUnavailable)` |
| Unexpected error while processing | `Failed(Unexpected)` |
| Cancel | Pending → `Cancelled` at once. Processing → sent to the Processor, `Cancelled` once it confirms. Already finished → `409`. A cancel racing a dispatch → the dispatcher sends the Processor a compensating cancel |
| Duplicate, late or out-of-order events | Ignored using the per-job sequence; the first final state wins |

## WebSocket disconnect / reconnect behavior

- The **Processor is the client**: it connects to the Job Service at `/ws/processor-events` and keeps one connection open.
- **While connected,** events are sent in order from a bounded channel with a single reader.
- **While disconnected,** the Processor keeps reading that channel and **discards** the events, so it never fills and processing never stalls. It reconnects with exponential backoff (1 s → 30 s) plus jitter.
- **On every (re)connect** it first sends a **snapshot**: one event per job it knows, stating that job's current status. The Processor updates its own job registry before publishing events, so the snapshot always supersedes anything that was discarded or lost mid-send.
- **The Job Service is idempotent:** duplicates and stale events are recognised by the per-job `sequence` and ignored, so delivery is at-least-once and applying is effectively once.
- **If events are lost for good** (e.g. the Processor restarted and forgot the job), the watchdog reconciles the job by asking the Processor or checking for the results blob.

## Logging & observability

- Structured `ILogger` logging with the job id in every message (and a `JobId` scope in the Processor), written to the console. In Docker that's what the container platform collects.
- Every processing event is logged, including the ones ignored as duplicates (at Debug level).
- `/health` on both services.
- `GET /api/processing-jobs/{jobId}` on the Processor shows its own view of a job.

## Known limitations & production improvements

- **No authentication between the services.** Anyone who can reach `/ws/processor-events` could send fake processing events, and anyone who can reach the Processor could submit or cancel work. In production: service-to-service auth (a client-credentials token checked on the WebSocket upgrade and on the Processor's API, or mTLS), with both kept on an internal network so only the Job Service's public API is exposed.
- **No idempotency on job creation.** A double click or a client retry creates a duplicate job. In production: an `Idempotency-Key` header. The server reserves `key → jobId` atomically; a repeat returns the original job, a concurrent repeat gets `409`, and the same key with a different body gets `422`.
- **No rate limiting.** Any number of jobs can be submitted; they wait in the dispatch line in memory. The Processor's job slots still bound the processing work. In production: ASP.NET Core rate limiting (`429`).
- **In-memory job state.** A Job Service restart loses all jobs (later events for them are logged and ignored). In production: SQLite or Postgres behind `IJobRepository`.
- **No retry endpoint.** A job that failed with `ProcessorUnavailable` or `ProcessorTimeout` stays failed. Next step: `POST /api/jobs/{id}/retry` (Failed → Pending), like replaying from a dead-letter queue.
- **A Processor restart abandons running jobs.** The watchdog marks them `Failed(ProcessorTimeout)` instead of requeueing them. Graceful shutdown with requeue, and incremental result writes for resuming, would fix this.
- **A Processor that is unreachable after the handover** leaves its jobs `Processing` until it returns; `lastUpdatedAt` shows how stale they are.
- **Progress is eventually consistent:** progress events are throttled and may be dropped; final states never are.
- **Rare double failure:** if both the HTTP replies and the WebSocket are down for the whole retry window, a job can be marked `Failed` although the Processor completed it.
- **No retention:** finished jobs and blobs are kept forever.
- **Documents are uploaded inside `POST /api/jobs`.** That is what caps a job at 10M characters and a 30 MB body. In production I'd apply the same claim-check as everywhere else: the client uploads the documents to blob storage (e.g. with a pre-signed URL) and the request carries only references. The size limit then becomes a storage concern, the Job Service never handles document contents, and the Processor could stream documents one at a time instead of loading a whole job.
- **One Processor instance.** Several would need a broker or a coordinator; a message broker is the production choice in general.
- **Console logging only;** no metrics, tracing or correlation ids yet.
- **No job priority:** strictly first in, first out.

## AI usage

**Tools.** Claude Code (Anthropic's Claude models) in the Claude desktop app.

**What it was used for.**
- **Design:** discussing alternatives and writing them down in [docs/PLAN.md](docs/PLAN.md). Every decision there was reviewed and decided by me, and several AI proposals were rejected (below).
- **Code:** from the document analyzer (stage 2) on, Claude wrote the tests and the implementation for each stage, test list first. I reviewed every change before it was committed. The skeleton (interfaces, contracts, DI) was also AI-assisted.
- **Docs and tooling:** this README, the Postman collection, the Docker files, LoadGen and the coverage script.

**Representative prompts** (lightly cleaned up), with what each one changed:
- *"Read the home assignment… I already wrote a rough draft of the architecture. Your mission is to help me spot blind spots in it, missing stuff, and plan the stages before actually starting to code."* This started a design review before any code was written. It surfaced the missing "Processor unreachable" handling, thread-safety bugs in my mutable `JobState`, and the false assumption that `Parallel.ForEachAsync` alone gives fairness between jobs.
- *"In a real-world scenario, messages should carry metadata."* followed by *"By a link to the blob storage sounds the best… but is it hard to implement here?"* This led to the claim-check design: `documents.json` / `results.json` in a shared blob store, with only references in requests and events.
- *"Why does this service need to enter the job into its own queue before working on it? Why not just have the Job Service send the jobs when the worker can actually work on them?"* This removed the Processor's intake queue. One waiting line remains, in the Job Service, and a busy Processor answers `503`.
- *"The Processor Service is the one who knows the job's progress state… Marking it Cancelled while the Processor is still processing seems like a lie… And I'm not sure why the Job Service needs to ingest the results at all."* This produced the "decide before the handover, record after it" rule, cancel as a command to the Processor, and the removal of the results-ingestion step.
- *"I just wouldn't rush to declare a job as Failed."* followed by *"I'm not too keen on endless retries; that's not something I'd do in production."* The result: bounded retries before the handover, no guessing after it, and a retry endpoint documented as the next step.
- *"Some of these are testing the same things from different classes… That looks like multiple things in charge of the same thing."* This exposed that the "already known?" check lived in two classes. Admission moved entirely into `ProcessingWorker.TryStart`, with one owner and one test per rule.
- *"I need you to explain the problem better before making any changes."* (on an intermittent test failure). Instead of the symptom fix it had started applying, the AI added temporary diagnostics and found the real cause: two shutdown paths racing, plus an exception escaping the WebSocket client during shutdown.

**AI suggestions I changed or rejected.**
- **Results inside WebSocket events.** The first design sent the analysis results in the `Completed` event. I rejected it: messages should carry metadata. That led to the claim-check design with `results.json`.
- **An `AwaitingResults` job state** while the Job Service read the results. Rejected: it's internal work, not part of the lifecycle.
- **A results-ingestion step.** The AI's design had a background service and a queue in the Job Service that, after `Finished`, verified `results.json` and copied the results into the job repository. I had it removed: the Processor writes the results atomically before reporting `Finished`, so the job becomes `Completed` directly, and `GET /api/jobs/{id}` reads the file on demand. This removed a background service, a queue, an error code and a second copy of every result.
- **A watchdog timeout that marks silent jobs `Failed`.** Rejected: after the handover, "unreachable" may be a network split with the Processor still working, so `Failed` would be a guess shown as fact. The staleness is shown through `lastUpdatedAt` instead.
- **A queue inside the Processor.** The AI's design gave the Processor its own intake queue, so the same jobs waited in two lines. I pushed for backpressure instead: the Processor accepts a job only when a slot is free and otherwise answers `503 Busy`, and the single waiting line stays in the Job Service.
- **Extra per-document metadata** beyond the four metrics the spec lists. Removed to keep to the spec.
