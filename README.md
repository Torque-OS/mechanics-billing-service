# mechanics-billing-service

Billing Service — one of the three microservices of the [Mechanics Software](https://github.com/Torque-OS/mechanics-software) Fase 4 architecture (FIAP POS Tech 15SOAT).

## Responsibility

Per [ADR-010](https://github.com/Torque-OS/mechanics-software/blob/main/docs/decisions/ADR-010-microservices-split.md):

- Generate and send budgets for a service order (using an item snapshot received from the OS Service).
- Record the customer's approval/rejection.
- Create and confirm payments via **Mercado Pago**, including webhooks.
- Publish `BudgetGenerated`, `BudgetApproved`, `BudgetRejected`, `PaymentConfirmed`, `PaymentFailed` events and react to `RequestBudget`, `RequestPayment`, `RefundPayment`, `CancelBudget` commands from the OS Service's Saga orchestrator ([ADR-011](https://github.com/Torque-OS/mechanics-software/blob/main/docs/decisions/ADR-011-saga-orquestrada-e-mensageria.md)).

This repo owns its own data — **no other service may query this database directly**
([ADR-012](https://github.com/Torque-OS/mechanics-software/blob/main/docs/decisions/ADR-012-banco-por-servico.md)): PostgreSQL for budgets/payments, MongoDB for raw Mercado Pago event/webhook payloads.

## Status

- **Budgets (F4-14): done.** Generate from the service order snapshot, approve/reject, query.
- **Saga messaging: interim.** Events (`BudgetGenerated`, `BudgetFailed`, `BudgetApproved`, `BudgetRejected`)
  are written as structured logs by `LoggingBudgetEventPublisher` until the broker (F4-08) and the
  MassTransit consumers/outbox (F4-13) exist. Until then, budgets are generated over HTTP.
- **Payments / Mercado Pago (F4-15+), MongoDB, API Gateway route and deploy (F4-33): not started.**

See the [Fase 4 board](https://github.com/orgs/Torque-OS/projects/4).

## Stack

C# 12 · ASP.NET Core 8 · PostgreSQL 16 · MongoDB · RabbitMQ + MassTransit (planned, ADR-011) · xUnit + FluentAssertions

## Running locally

```bash
docker compose up --build
```

The API listens on `http://localhost:8081`, with `GET /health` for a liveness check. PostgreSQL is exposed on `5436` and MongoDB on `27018` to avoid clashing with `mechanics-software`'s own local stack.
Migrations are applied on startup.

Without Docker for the API: `docker compose up postgres -d`, then
`dotnet run --project src/MechanicsSoftware.BillingService.Api` (Swagger at `/swagger` in Development).

## Endpoints

All `/api/budgets` routes require `Authorization: Bearer <JWT>` — the same platform token issued by
`mechanics-software` (staff, `POST /api/auth/login`) and `mechanics-lambda` (customers, `POST /auth`).
Errors use `{ "error": "..." }`.

| Method | Route | Who | Result |
|---|---|---|---|
| `POST` | `/api/budgets` | Staff | `201` new budget · `200` existing one for the same service order (idempotent) · `422` invalid snapshot (`BudgetFailed`) |
| `GET` | `/api/budgets/{id}` | Staff | `200` · `404` |
| `GET` | `/api/budgets?serviceOrderId={id}` | Staff | `200` · `404` · `400` without the parameter |
| `POST` | `/api/budgets/{id}/decision` | Customer (own budget) or staff | `200` · `403` another customer's budget · `404` · `409` already decided |

```http
POST /api/budgets
{
  "serviceOrderId": "<guid>",
  "customerId": "<guid>",
  "items": [
    { "type": "Service", "referenceId": "<guid>", "description": "Oil change", "unitPriceInCents": 15000, "quantity": 1 },
    { "type": "Part",    "referenceId": "<guid>", "description": "Oil filter", "unitPriceInCents": 4500,  "quantity": 2 }
  ]
}

POST /api/budgets/{id}/decision
{ "decision": "approve" }        // or "reject"
```

A budget needs at least one `Service` item. "Sending" the budget for approval is the `BudgetGenerated`
event: the OS Service moves the order to `AWAITING_APPROVAL` and e-mails the customer.

> **Contract for the OS Service (F4-13):** the `RequestBudget` message must carry the same snapshot as
> `POST /api/budgets` — `serviceOrderId`, **`customerId`** and the items. `customerId` is required: it is how
> the Billing Service checks that a customer only decides their own budget (there is no access to the OS
> database, ADR-012). Without it, generation fails with `BudgetFailed` and the Saga cancels the order.

## Configuration

| Variable | Required | Description |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Yes | PostgreSQL (`billing` database) |
| `JWT_SECRET` | Yes | **Must match `mechanics-software` and `mechanics-lambda`** |
| `JWT_ISSUER` / `JWT_AUDIENCE` | No | Default `torque-os` / `mechanics-software-api` — must match the token issuers |

Development values live in `appsettings.Development.json` and `docker-compose.yml`; production values come
from `k8s/secret.yaml` (rendered from CI secrets).

## Database migrations

`dotnet-ef` is pinned in `.config/dotnet-tools.json` (`dotnet tool restore` once).

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet dotnet-ef migrations add <Name> \
  --project src/MechanicsSoftware.BillingService.Infrastructure/MechanicsSoftware.BillingService.Infrastructure.csproj \
  --startup-project src/MechanicsSoftware.BillingService.Api/MechanicsSoftware.BillingService.Api.csproj \
  --output-dir Persistence/Migrations
```

## Testing

```bash
dotnet test
```

Domain, application and persistence tests, plus endpoint tests that run the real HTTP pipeline
(JWT, policies, error mapping) over EF InMemory — no PostgreSQL or broker needed.

## Project layout

```
src/MechanicsSoftware.BillingService.Domain/          Budget aggregate, value objects, policies (no framework dependencies)
src/MechanicsSoftware.BillingService.Application/     Commands, handlers, queries, abstractions
src/MechanicsSoftware.BillingService.Infrastructure/  EF Core (PostgreSQL), migrations, JWT, interim event publisher
src/MechanicsSoftware.BillingService.Api/             Controllers, middleware, Swagger, composition root
tests/MechanicsSoftware.BillingService.UnitTests/
k8s/                                        Namespace, Deployment, Service, ConfigMap, Secret, HPA
.github/workflows/ci.yml                    Build, test, Docker build (SonarQube + deploy: F4-30/F4-33)
```

## Contributing

`main` is protected — open a PR from a branch and get one approval. Conventional commits.
