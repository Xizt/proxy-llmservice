# LLM Shadow Proxy

A production-ready .NET 8 shadow-testing system that proxies customer chat traffic to a primary LLM while concurrently routing the same requests to a candidate model for comparison. All four components are independent Docker images deployable on DigitalOcean.

## Architecture

```
Client
  └── POST /v1/chat ──▶ ProxyService (DO App Platform)
                            │
                            ├── stream ──▶ Primary LLM (DO Inference)
                            │                  │
                            │           write RequestRecord +
                            │           PrimaryLlmResponse
                            │                  │
                            │                  ▼
                            │              PostgreSQL
                            │
                            └── XADD ──▶ Redis Stream (DO Managed Redis)
                                               │
                              XREADGROUP ──────┘
                                    │
                          SecondaryProcessor (Docker Worker)
                                    │
                                    ├── call ──▶ Candidate LLM (DO Inference)
                                    │
                                    └── write SecondaryLlmResponse ──▶ PostgreSQL
                                                                             │
                                                             Evaluator (Docker Worker)
                                                             (PeriodicTimer every N seconds)
                                                                             │
                                                             fetch unevaluated pairs,
                                                             compare action keys,
                                                             set Matched/Failed
                                                                             │
                                                                             ▼
                                                                         PostgreSQL

Internal Users
  └── GET /metrics ──▶ CompareService (DO App Platform)
                            └── aggregate query ──▶ PostgreSQL
```

## Components

| Project | Type | Deployment |
|---|---|---|
| `LlmShadow.ProxyService` | ASP.NET Core API | DO App Platform (Web Service) |
| `LlmShadow.SecondaryProcessor` | .NET Worker Service | DO App Platform (Worker) |
| `LlmShadow.Evaluator` | .NET Worker Service | DO App Platform (Worker) |
| `LlmShadow.CompareService` | ASP.NET Core API | DO App Platform (Web Service) |

## Shared Libraries

| Project | Purpose |
|---|---|
| `LlmShadow.Common` | `ServiceResult<T>`, options POCOs, exceptions, correlation ID |
| `LlmShadow.Models` | Request/response DTOs, `ShadowQueueMessage`, `RequestStatus` enum |
| `LlmShadow.DataLayer` | EF Core + Npgsql, entities, repositories, migrations |
| `LlmShadow.Inference` | OpenAI-compatible DO Inference HTTP client (streaming + non-streaming) |
| `LlmShadow.Messaging` | Redis Streams publisher/consumer with consumer groups and dead-letter |
| `LlmShadow.Evaluation` | `JsonActionEvaluator` — deterministic `action`-key comparison |

## Database Schema

Three tables in PostgreSQL DB (code-first via EF Core migrations):

- **Requests** — request lifecycle, status, evaluation outcome
- **PrimaryLlmResponses** — buffered primary streaming response + latency
- **SecondaryLlmResponses** — candidate model response + latency

## Quick Start (local)

### Prerequisites

- .NET 8 SDK
- Docker (for PostgreSQL and Redis)

### 1. Start infrastructure

```bash
# PostgreSQL
docker run -d --name shadow-pg \
  -e POSTGRES_USER=shadow \
  -e POSTGRES_PASSWORD=shadow \
  -e POSTGRES_DB=shadowdb \
  -p 5432:5432 postgres:16

# Redis
docker run -d --name shadow-redis \
  -p 6379:6379 redis:7
```

### 2. Configure secrets

Set environment variables (or use `appsettings.Development.json`):

```bash
export Database__ConnectionString="Host=localhost;Database=shadowdb;Username=shadow;Password=shadow"
export Redis__ConnectionString="localhost:6379"
export Inference__ModelAccessKey="your-do-model-access-key"
export Inference__PrimaryModel="meta-llama/Meta-Llama-3.1-8B-Instruct"
export Inference__CandidateModel="meta-llama/Meta-Llama-3.1-70B-Instruct"
```

### 3. Run services

```bash
# Terminal 1 — hot-path proxy
dotnet run --project src/Services/LlmShadow.ProxyService

# Terminal 2 — shadow worker
dotnet run --project src/Services/LlmShadow.SecondaryProcessor

# Terminal 3 — evaluator
dotnet run --project src/Services/LlmShadow.Evaluator

# Terminal 4 — metrics API
dotnet run --project src/Services/LlmShadow.CompareService
```

### 4. Test

```bash
# Send a chat request (streaming SSE)
curl -X POST http://localhost:5000/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"Reply with only {\"action\":\"greet\"}"}]}'

# Check metrics
curl http://localhost:5001/metrics
```

## Building Docker Images

All `Dockerfile`s use multi-stage builds and must be built from the **repository root** so the shared libraries are accessible:

```bash
# ProxyService
docker build -f src/Services/LlmShadow.ProxyService/Dockerfile \
  -t registry.digitalocean.com/<your-registry>/proxy-service:latest .

# SecondaryProcessor
docker build -f src/Services/LlmShadow.SecondaryProcessor/Dockerfile \
  -t registry.digitalocean.com/<your-registry>/secondary-processor:latest .

# Evaluator
docker build -f src/Services/LlmShadow.Evaluator/Dockerfile \
  -t registry.digitalocean.com/<your-registry>/evaluator:latest .

# CompareService
docker build -f src/Services/LlmShadow.CompareService/Dockerfile \
  -t registry.digitalocean.com/<your-registry>/compare-service:latest .
```

## Running Tests

```bash
dotnet test tests/LlmShadow.UnitTests
```

## EF Core Migrations

To add a new migration after modifying entities:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Shared/LlmShadow.DataLayer \
  --startup-project src/Services/LlmShadow.ProxyService
```

## Environment Variables Reference

All secrets must be supplied via environment variables in production. Never commit credentials.

| Variable | Description |
|---|---|
| `Database__ConnectionString` | DO Managed PostgreSQL connection string |
| `Redis__ConnectionString` | DO Managed Redis connection string |
| `Inference__ModelAccessKey` | DO Inference model access key |
| `Inference__PrimaryModel` | Primary LLM model ID |
| `Inference__CandidateModel` | Candidate (shadow) LLM model ID |
| `Inference__PrimaryTimeoutSeconds` | Streaming timeout for primary calls (default: 60) |
| `Inference__CandidateTimeoutSeconds` | Timeout for candidate calls (default: 120) |
| `Evaluator__IntervalSeconds` | Evaluator polling interval (default: 60) |
| `Evaluator__BatchSize` | Records per evaluation cycle (default: 100) |
| `Processor__MaxDegreeOfParallelism` | Concurrent shadow executions (default: 4) |
| `Redis__MaxRetryCount` | Dead-letter threshold (default: 3) |

## CI/CD Pipeline

Two GitHub Actions workflows in `.github/workflows/` implement build/release separation:

| Workflow | Trigger | What it does |
|---|---|---|
| `build.yml` | Push/PR to `main`, manual | Restores + builds `ProxyLlmService.sln`, runs `LlmShadow.UnitTests`, and (on push only) builds & pushes all 4 component images to DOCR tagged with a build id (short git SHA) |
| `release.yml` | Manual (`workflow_dispatch`) | Takes a `build_id` + `environment` (dev/int/prod) input, verifies the images exist, then applies the matching `infra/app-platform/<environment>.app.yaml` spec via `doctl`/`digitalocean/app_action` to redeploy that exact build |

Deploying is always a deliberate, two-step action: run **Build** to produce and publish a build id, then run **Release** with that `build_id` and the target `environment`.

### DigitalOcean App Platform specs

`infra/app-platform/{dev,int,prod}.app.yaml` each declare one DO App per environment (`llmshadow-dev`, `llmshadow-int`, `llmshadow-prod`) containing all 4 components: `proxy-service` and `compare-service` as web services, `secondary-processor` and `evaluator` as workers. Instance sizes/counts scale up per environment. Every environment-specific or sensitive value is a `${PLACEHOLDER}` — nothing is hardcoded or committed.

### One-time repository setup (no secrets in the pipeline itself)

1. **Create 3 GitHub Environments**: Settings → Environments → `dev`, `int`, `prod`.
   - On `prod`, add required reviewers so every production release needs manual approval — this is the environment-level protection GitHub enforces before the `deploy` job runs, independent of the workflow YAML.
2. **Per-environment secrets** (Settings → Environments → `<env>` → Secrets): `DIGITALOCEAN_ACCESS_TOKEN`, `DATABASE_CONNECTION_STRING`, `REDIS_CONNECTION_STRING`, `INFERENCE_MODEL_ACCESS_KEY`.
3. **Per-environment variables** (Settings → Environments → `<env>` → Variables): `INFERENCE_PRIMARY_MODEL`, `INFERENCE_CANDIDATE_MODEL`.
4. **Repository-level secret** (Settings → Secrets and variables → Actions → Secrets): `DIGITALOCEAN_ACCESS_TOKEN` — a registry-scoped token used only by `build.yml` to push images to DOCR.
5. **Repository-level variable**: `DO_REGISTRY` — your DOCR registry name.
6. **Bootstrap each DO App once** (first time only, out of band): `doctl apps create --spec infra/app-platform/dev.app.yaml` (repeat for `int`/`prod`) so the apps referenced by the specs exist before the first `release.yml` run.

Because every secret lives in GitHub's encrypted, environment-scoped secret store and is only ever referenced as `${{ secrets.X }}` — never hardcoded, echoed, or committed — no credentials are stored in the pipeline definitions or the repository.
