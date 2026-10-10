# Happy Flow

One job, from the client's POST to `Completed`, with nothing going wrong. The failure paths are in [PLAN.md](PLAN.md#reliability).

**Who decides what:** the Job Service decides only what happens before the handover (waiting in line). Once the Processor accepts the job, the Job Service records what the Processor reports. The Processor *finishes* its work; the Job Service marks the job *Completed* when it receives that report.

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant JS as Job Service<br/>(JobManager)
    participant Blob as Blob store<br/>(shared folder)
    participant Disp as JobDispatcher<br/>(Job Service, background)
    participant Mgr as ProcessingJobManager<br/>(Processor)
    participant Worker as ProcessingWorker<br/>(Processor)
    participant Events as ProcessingEventHandler<br/>(Job Service, WebSocket)

    rect rgba(100, 150, 255, 0.12)
    Note over Client,Blob: Phase 1 — Create (synchronous HTTP request)
    Client->>JS: POST /api/jobs { documents[] }
    JS->>JS: validate request
    JS->>Blob: write jobs/{id}/documents.json
    JS->>JS: store JobMetadata + PendingState
    JS->>Disp: put jobId in the dispatch line
    JS-->>Client: 202 Accepted + Location + JobSummary
    Note over JS: Job Service: Pending
    end

    rect rgba(255, 180, 80, 0.12)
    Note over Disp,Worker: Phase 2 — Hand over (Job Service background)
    Disp->>Disp: take job at the head of the line, still Pending?
    Disp->>Mgr: POST /api/processing-jobs { jobId, documentsRef, documentCount }
    Mgr->>Mgr: validate request
    Mgr->>Worker: TryStart(job)
    Worker->>Worker: not already known, free job slot → register job
    Note over Worker: Processor: Processing
    Worker-->>Mgr: Started
    Mgr-->>Disp: 202 Accepted (the Processor has the job)
    Disp->>Disp: Pending ➜ Processing
    Note over Disp: Job Service: Processing
    end

    rect rgba(120, 200, 120, 0.12)
    Note over Worker,Events: Phase 3 — Process (Processor background, events over WebSocket)
    Worker->>Events: WS ProcessingStarted (seq 1)
    Events->>Events: Apply → already Processing, logged
    Worker->>Blob: read jobs/{id}/documents.json
    loop each document (≤ 4 per job, ≤ cores globally)
        Worker->>Worker: Analyze → result kept in memory (array slot per document)
    end
    Worker->>Events: WS ProcessingProgressUpdated (seq 2…n, throttled)
    Events->>Events: Apply → progress updated
    Worker->>Blob: write jobs/{id}/results.json (once, atomic)
    Worker->>Worker: mark Finished, free the job slot
    Note over Worker: Processor: Finished
    Worker->>Events: WS ProcessingFinished { resultsRef } (seq n+1)
    Events->>Events: Apply → Processing ➜ Completed
    Note over Events: Job Service: Completed
    Events->>Disp: SlotFreed() → next waiting job is sent now
    end

    rect rgba(160, 160, 160, 0.12)
    Note over Client,Blob: Phase 4 — Read (any time)
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
| Processor accepted the job (step 11) | `Processing` | `Pending` (202 not received yet) |
| dispatcher got the 202 (steps 13–14) | `Processing` | `Processing`, 0 / N |
| `ProcessingProgressUpdated` applied (steps 19–20) | `Processing` | `Processing`, k / N |
| Processor done (steps 21–23) | `Finished` | `Processing` (event on its way) |
| `ProcessingFinished` applied (step 24) | `Finished` | **`Completed`**, N / N, results readable |

`ProcessingStarted` and the dispatcher's 202 can arrive in either order: whichever comes first moves the job to Processing, and the other is a no-op (but still logged).

## Who talks to whom

| From → To | Channel | Carries |
|---|---|---|
| Client → Job Service | REST | the documents (validated, then stored as a blob) |
| Job Service → Processor | REST | `jobId` + `documentsRef` only (claim-check); cancel commands |
| Processor → Job Service | WebSocket | processing events with metadata only (counts, `resultsRef`) |
| Both services ↔ blob store | shared folder | `documents.json` (written by the Job Service), `results.json` (written by the Processor, read by the Job Service on demand) |
