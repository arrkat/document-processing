# Happy Flow

One job, from the client's POST to `Completed`, with nothing going wrong. The failure paths are in [PLAN.md](PLAN.md#reliability).

**Vocabulary:** the Processor *finishes* its work. The Job Service *completes* the job, after it has verified the results. These are two different moments.

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant JS as Job Service<br/>(JobManager)
    participant Blob as Blob store<br/>(shared folder)
    participant Disp as JobDispatcher<br/>(Job Service, background)
    participant Intake as JobIntake<br/>(Processor)
    participant Worker as ProcessingWorker<br/>(Processor)
    participant Events as ProcessingEventHandler<br/>(Job Service, WebSocket)
    participant Ingest as ResultsIngestor<br/>(Job Service, background)

    rect rgba(100, 150, 255, 0.12)
    Note over Client,Blob: Phase 1 — Create (synchronous HTTP request)
    Client->>JS: POST /api/jobs { documents[] }
    JS->>JS: validate request
    JS->>Blob: write jobs/{id}/documents.json
    JS->>JS: store JobMetadata + PendingState
    JS->>Disp: enqueue jobId on the dispatch line
    JS-->>Client: 202 Accepted + Location + JobSummary
    Note over JS: Job Service: Pending
    end

    rect rgba(255, 180, 80, 0.12)
    Note over Disp,Worker: Phase 2 — Dispatch (background)
    Disp->>Disp: take job at the head of the line, still Pending?
    Disp->>Intake: POST /api/processing-jobs { jobId, documentsRef, documentCount }
    Intake->>Intake: validate, not already known
    Intake->>Worker: TryStart(job)
    Worker->>Worker: take a free job slot, register job
    Note over Worker: Processor: Processing
    Worker-->>Intake: Started
    Intake-->>Disp: 202 Accepted
    end

    rect rgba(120, 200, 120, 0.12)
    Note over Worker,Events: Phase 3 — Process (Processor, background)
    Worker->>Events: WS ProcessingStarted (seq 1)
    Events->>Events: Apply → Pending ➜ Processing
    Note over Events: Job Service: Processing
    Worker->>Blob: read jobs/{id}/documents.json
    loop each document (≤ 4 per job, ≤ cores globally)
        Worker->>Worker: Analyze → result kept in memory (array slot per document)
    end
    Worker->>Events: WS ProcessingProgress (seq 2…n, throttled)
    Events->>Events: Apply → progress updated
    Worker->>Blob: write jobs/{id}/results.json (once, atomic)
    Worker->>Worker: mark Finished, free the job slot
    Note over Worker: Processor: Finished
    Worker->>Events: WS ProcessingFinished { resultsRef } (seq n+1)
    end

    rect rgba(190, 130, 230, 0.12)
    Note over Events,Ingest: Phase 4 — Verify results (Job Service, background)
    Events->>Events: Apply → still Processing (sequence bumped)
    Note over Events: Processor: Finished / Job Service: still Processing
    Events->>Ingest: enqueue jobId for verification
    Events->>Disp: SlotFreed() → next waiting job is sent now
    Ingest->>Blob: read jobs/{id}/results.json, check one result per document
    Ingest->>Ingest: ProcessingState.Complete
    Note over Ingest: Job Service: Completed (results stay in the blob)
    end

    rect rgba(160, 160, 160, 0.12)
    Note over Client,Blob: Phase 5 — Read (any time)
    Client->>JS: GET /api/jobs/{id}
    JS->>JS: state from memory → JobSummary
    JS->>Blob: read jobs/{id}/results.json (Completed jobs only)
    JS-->>Client: 200 JobDetails (Completed + results)
    end
```

## State on each side

| Moment | Processor (`ProcessingStatus`) | Job Service (`JobStatus`, what `GET` returns) |
|---|---|---|
| right after POST (step 6) | — (doesn't know the job yet) | `Pending`, 0 / N processed |
| Processor accepted the job (step 11) | `Processing` | `Pending` (event not arrived yet) |
| `ProcessingStarted` applied (steps 14–15) | `Processing` | `Processing`, 0 / N |
| `ProcessingProgress` applied (steps 18–19) | `Processing` | `Processing`, k / N |
| Processor done (steps 21–23) | **`Finished`** | **`Processing`**, N / N |
| results verified (steps 26–27) | `Finished` | **`Completed`**, N / N, results readable |

## Who talks to whom

| From → To | Channel | Carries |
|---|---|---|
| Client → Job Service | REST | the documents (validated, then stored as a blob) |
| Job Service → Processor | REST | `jobId` + `documentsRef` only (claim-check) |
| Processor → Job Service | WebSocket | processing events with metadata only (counts, `resultsRef`) |
| Both services ↔ blob store | shared folder | `documents.json` (written by the Job Service), `results.json` (written by the Processor, read by the Job Service on demand) |
