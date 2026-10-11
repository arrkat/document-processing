# TA9 – Real-Time Document Processing System

Two .NET 8 services. The **Job Service** accepts jobs and tracks their state. The **Processor Service** analyses the documents concurrently. They talk over REST (Job Service → Processor) and WebSocket (Processor → Job Service). Document and result payloads travel through a shared blob store (claim-check pattern). Requests and events carry only metadata and blob references.

## Build & run

Requires .NET SDK 8.0.4xx (pinned in `global.json`).

```bash
dotnet build
```

Run each service in its own terminal:

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

## Tests

```bash
dotnet test
```

Code coverage (coverlet + ReportGenerator, restored as a local .NET tool) runs every test project and merges the results into `TestResults/coverage-report/index.html`:

```bash
scripts/coverage.sh
```

## Architecture overview

_TODO_

## Key technical decisions & trade-offs

_TODO_

## Concurrency & performance

_TODO_

## WebSocket disconnect / reconnect behavior

_TODO_

## Known limitations & production improvements

- **No idempotency on job creation.** Each `POST /api/jobs` creates a new job, so a double click or a client retry after a timeout creates duplicate jobs. In production: an optional `Idempotency-Key` header, with the details below.
  - The server atomically reserves `key → jobId` after validation.
  - A repeat with the same key returns the original job.
  - A concurrent in-flight repeat gets `409`.
  - The same key with a different body gets `422`.
  - The key is released if creation fails.
- **No rate limiting.** Overload is handled only by bounded queues (`503`). In production: the ASP.NET Core rate-limiting middleware (`429`).

_TODO: remaining items_

## API naming

The spec's `/api/process` is exposed as `/api/processing-jobs`, because REST paths name resources (nouns), not actions. Cancellation is `POST …/{id}/cancel` on both services, because the job is not deleted, only stopped.

## AI usage

_TODO_
