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
- **Payments with Mercado Pago (F4-15): done.** Dynamic QR via the Orders API, signed webhook, verification
  against Mercado Pago, raw notifications in MongoDB.
- **Saga events published (F4-16): done.** MassTransit v8 + RabbitMQ with a transactional outbox — see
  [Saga messages](#saga-messages). Payment history in MongoDB (`payment_events`). Consuming them in the OS Service
  is F4-13; until then budgets are generated over HTTP and the OS is not updated automatically.
- **Refund / cancellation compensations (F4-17), API Gateway route and deploy (F4-33): not started.**
  Until the gateway route exists, Mercado Pago webhooks only reach a local stack through a tunnel (see below).

See the [Fase 4 board](https://github.com/orgs/Torque-OS/projects/4).

## Stack

C# 12 · ASP.NET Core 8 · PostgreSQL 16 · MongoDB · RabbitMQ + MassTransit 8.5 (ADR-011) · xUnit + FluentAssertions

## Running locally

```bash
docker compose up --build
```

The API listens on `http://localhost:8081`, with `GET /health` for a liveness check. PostgreSQL is exposed on `5436` and MongoDB on `27018` to avoid clashing with `mechanics-software`'s own local stack.
RabbitMQ runs on the default `5672`, with the management UI at `http://localhost:15672` (`guest` / `guest`).
Migrations are applied on startup.

Without Docker for the API: `docker compose up postgres mongo rabbitmq -d`, then
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
| `POST` | `/api/payments` | Customer (own budget) or staff | `201` new payment with `qrData` · `200` existing one for the budget · `409` budget not approved · `502` Mercado Pago failure |
| `GET` | `/api/payments/{id}` | Staff | `200` · `404` |
| `POST` | `/api/payments/{id}/verify` | Staff | `200` after re-reading the order from Mercado Pago · `502` |
| `GET` | `/api/payments/{id}/qr.png` | **Public** | `200` QR image to scan · `409` payment no longer pending |
| `POST` | `/api/payments/webhooks/mercadopago` | **Mercado Pago** (`x-signature`) | `200` · `401` invalid signature |

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

## Payments (Mercado Pago)

```
POST /api/payments { "budgetId": "<approved budget>" }
  → Mercado Pago POST /v1/orders (type qr, mode dynamic, 15 min) → qrData
GET /api/payments/{id}/qr.png → customer scans with the Mercado Pago app and pays
Mercado Pago → POST /api/payments/webhooks/mercadopago (topic "Order")
  → signature checked → raw body stored in MongoDB (mercadopago_notifications)
  → GET /v1/orders/{id}: processed → CONFIRMED + PaymentConfirmed · expired/canceled/failed → FAILED + PaymentFailed
```

- **The webhook body is never trusted**: the outcome always comes from `GET /v1/orders/{id}`. Repeated or
  out-of-order notifications are harmless — a payment changes status (and publishes an event) only once.
- `POST /api/payments/{id}/verify` does the same check on demand, for when a webhook does not arrive.
- `qr.png` is public on purpose (like the OS status link): the id is an unguessable GUID and the image only
  lets someone pay this charge.
- One payment per budget, for the approved total. An expired QR fails the payment (and, through the Saga,
  the order) — see F4-17 for compensations.

### Sandbox setup (once)

1. Mercado Pago **Your integrations** → create an application → copy the **test** Access Token.
2. Create the **test users** (seller and buyer); log in to the Mercado Pago app with the **buyer**.
3. `cp env.example .env`, paste the token, then run `scripts/setup-mercadopago-sandbox.sh` (creates the store and the POS).
4. Start a tunnel to the local API: `cloudflared tunnel --url http://localhost:8081` and, in **Webhooks**, set
   `<tunnel-url>/api/payments/webhooks/mercadopago` with the topic **Order (Mercado Pago)**; copy the
   **secret signature** into `.env`. The tunnel URL changes on every run.
5. `docker compose up --build`, approve a budget, `POST /api/payments`, open `qr.png` and pay with the buyer account.
6. Use **Simulate** in the Webhooks panel to check URL + secret (expect `200 {"outcome":"Ignored"}`).

> **Sandbox limitation:** the test **seller** account (the owner of the test Access Token, who receives the payment)
> cannot configure webhooks, so a real test purchase does not notify your application. After paying, either call
> `POST /api/payments/{id}/verify`, or use **Simulate** with the real order id (`providerOrderId`) as *Data ID* — the
> notification is signed by Mercado Pago and the service confirms the payment from `GET /v1/orders/{id}`.
> Note: Mercado Pago signs `data.id` exactly as received (upper-case `ORD…`), not lower-cased as the docs state.

## Saga messages

The Billing Service **publishes** these events (ADR-011) with MassTransit 8.5 over RabbitMQ. They go through the
**EF Core transactional outbox**: the message is stored in PostgreSQL in the same transaction as the state change and
delivered after it commits — a crash never loses an event, and a rolled-back change never emits one.

| Message (`TorqueOS.Contracts.Billing`) | Fields | Published when |
|---|---|---|
| `BudgetGenerated` | `ServiceOrderId`, `BudgetId`, `TotalInCents`, `OccurredAt` | budget created |
| `BudgetFailed` | `ServiceOrderId`, `Reason`, `OccurredAt` | snapshot rejected (e.g. no service item) |
| `BudgetApproved` | `ServiceOrderId`, `BudgetId`, `OccurredAt` | customer/staff approved |
| `BudgetRejected` | `ServiceOrderId`, `BudgetId`, `OccurredAt` | customer/staff rejected |
| `PaymentConfirmed` | `ServiceOrderId`, `PaymentId`, `BudgetId`, `AmountInCents`, `OccurredAt` | Mercado Pago `processed` |
| `PaymentFailed` | `ServiceOrderId`, `PaymentId`, `BudgetId`, `Reason`, `OccurredAt` | order `expired` / `canceled` / `failed` |

**For consumers (OS Service, F4-13):**

- Declare **records with exactly this namespace, name and fields** — MassTransit identifies a message by
  `urn:message:TorqueOS.Contracts.Billing:<Name>` (pinned by `SagaContractTests`). Copy them from
  `src/MechanicsSoftware.BillingService.Application/Contracts/`.
- Each message has its own fanout exchange named `TorqueOS.Contracts.Billing:<Name>`; a MassTransit consumer of the
  same type binds its queue automatically.
- Envelope: `CorrelationId` = `ServiceOrderId`; `MessageId` is unique per message — **deduplicate on it**
  (delivery is at-least-once).
- Money in cents (`int`), timestamps in UTC.
- Evolution: fields are only ever added. A breaking change gets a new type (e.g. `PaymentConfirmedV2`).

**Payment history:** every payment transition (`created`, `confirmed`, `failed`) is appended to MongoDB
`payment_events` with the Mercado Pago order as read at that moment (`providerSnapshot`). Raw webhook bodies stay in
`mercadopago_notifications`.

## Configuration

| Variable | Required | Description |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Yes | PostgreSQL (`billing` database) |
| `JWT_SECRET` | Yes | **Must match `mechanics-software` and `mechanics-lambda`** |
| `JWT_ISSUER` / `JWT_AUDIENCE` | No | Default `torque-os` / `mechanics-software-api` — must match the token issuers |
| `ConnectionStrings__Mongo` | Yes | MongoDB: raw Mercado Pago notifications and payment history (database from the URL, default `billing`) |
| `RABBITMQ_CONNECTION_STRING` | Yes | `amqp://user:password@host:5672/` — Saga broker (ADR-011) |
| `MERCADOPAGO_ACCESS_TOKEN` | For payments | Access Token (test credentials in sandbox). Missing → payment calls return `502` |
| `MERCADOPAGO_WEBHOOK_SECRET` | For webhooks | Secret signature of the webhook. Missing → every notification is rejected (`401`) |
| `MERCADOPAGO_EXTERNAL_POS_ID` | For payments | `external_id` of the POS (`TORQUEOSPOS001` from the setup script) |
| `MERCADOPAGO_QR_EXPIRATION` | No | ISO-8601 duration, default `PT15M` |

Development values live in `appsettings.Development.json` and `docker-compose.yml`; production values come
from `k8s/secret.yaml` (rendered from CI secrets). **Mercado Pago credentials are never in versioned files**:
locally they live in `.env` (gitignored, read by docker compose; template in `env.example`).

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
src/MechanicsSoftware.BillingService.Domain/          Budget and Payment aggregates, value objects, policies (no framework dependencies)
src/MechanicsSoftware.BillingService.Application/     Commands, handlers, queries, abstractions
src/MechanicsSoftware.BillingService.Infrastructure/  EF Core (PostgreSQL), MongoDB, Mercado Pago client, migrations, JWT, interim event publishers
src/MechanicsSoftware.BillingService.Api/             Controllers, middleware, Swagger, composition root
scripts/setup-mercadopago-sandbox.sh                  One-time sandbox store + POS creation
tests/MechanicsSoftware.BillingService.UnitTests/
k8s/                                        Namespace, Deployment, Service, ConfigMap, Secret, HPA
.github/workflows/ci.yml                    Build, test, Docker build (SonarQube + deploy: F4-30/F4-33)
```

## Contributing

`main` is protected — open a PR from a branch and get one approval. Conventional commits.
