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

Three tables in PostgreSQL (code-first via EF Core migrations):

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
