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

**Scaffold only** (F4-06). The domain/application/infrastructure layers, the Mercado Pago
integration and the Saga event handlers are tracked in F4-14 to F4-17. See the
[Fase 4 board](https://github.com/orgs/Torque-OS/projects/4).

## Stack

C# 12 · ASP.NET Core 8 · PostgreSQL 16 · MongoDB · RabbitMQ + MassTransit (planned, ADR-011) · xUnit + FluentAssertions

## Running locally

```bash
docker compose up --build
```

The API listens on `http://localhost:8081`, with `GET /health` for a liveness check. PostgreSQL is exposed on `5436` and MongoDB on `27018` to avoid clashing with `mechanics-software`'s own local stack.

## Project layout

```
src/MechanicsSoftware.BillingService.Api/   ASP.NET Core Web API (health endpoint only, for now)
tests/MechanicsSoftware.BillingService.UnitTests/
k8s/                                        Namespace, Deployment, Service, ConfigMap, Secret, HPA
.github/workflows/ci.yml                    Build, test, coverage gate, SonarCloud, Docker build (F4-30)
.github/workflows/deploy.yml                Push image to GHCR, deploy to EKS (F4-33)
```

## CI/CD

- **`ci.yml`** (F4-30) — on every push/PR to `main`: build, run unit tests with coverage, enforce
  an 80% line-coverage gate, run a SonarCloud scan, and build the Docker image. The SonarCloud
  steps are `continue-on-error`, so a missing `SONAR_TOKEN` degrades to "no quality report" rather
  than failing the whole pipeline — **requires**:
  - The `SONAR_TOKEN` repo secret.
  - A SonarCloud project with key `rnataoliveira_mechanics-billing-service` under the
    `rnataoliveira` org (auto-provisioned on first scan if using SonarCloud's GitHub-connected
    CI-based analysis; otherwise create it manually first).
- **`deploy.yml`** (F4-33) — on a successful `ci.yml` run on `main`: build and push the image to
  GHCR, then apply the `k8s/` manifests to the `mechanics-billing` namespace of the shared EKS
  cluster (the same cluster `mechanics-software` deploys to, per ADR-010 — provisioned by
  [`mechanics-infra-k8s`](https://github.com/Torque-OS/mechanics-infra-k8s), F4-08/F4-09) —
  **requires** these repo secrets: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` (same IAM user as
  `mechanics-software`), `DB_HOST`, `DB_USER`, `DB_PASSWORD`, `MONGO_CONNECTION_STRING`,
  `JWT_SECRET`, `MERCADOPAGO_ACCESS_TOKEN`, `MERCADOPAGO_WEBHOOK_SECRET`,
  `MERCADOPAGO_EXTERNAL_POS_ID`, `RABBITMQ_CONNECTION_STRING`. None of these are set yet, and the
  EKS cluster isn't currently running — `deploy.yml` will fail at the `aws eks update-kubeconfig`
  step until both exist. The pipeline is ready; the infrastructure to run it against isn't.

## Contributing

`main` is protected — open a PR from a branch and get one approval. Conventional commits.
