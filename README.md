# FicMart Payment Gateway

FicMart Payment Gateway is a C#/.NET 10 service built to explore the problems behind reliable payment processing. It sits between FicMart and an intentionally unreliable mock bank, keeps its own durable payment records in PostgreSQL, and avoids reporting a false success or failure when the bank response is uncertain.

## Current Status

The complete version-one lifecycle is implemented:

```text
PendingAuthorization -> Authorized -> Captured -> Refunded
                              |
                              +------> Voided
```

The gateway currently supports:

- operation-scoped idempotency for every money-moving request
- bounded retries for known transient bank failures
- explicit unknown outcomes for timeouts and lost responses
- recovery of abandoned and uncertain operations after a restart
- database-enforced protection against capture-versus-void races
- API-key authentication, correlation IDs, audit history, and metrics
- Docker-based local setup and automated CI verification

Version one supports USD, full capture, full void, and full refund. Partial operations and customer payment history are out of scope.

## Run With Docker

```bash
export FICMART_API_KEY='replace-with-at-least-32-characters'
export GATEWAY_FINGERPRINT_SECRET='replace-with-at-least-32-characters'
docker compose -f docker/docker-compose.yaml up --build
```

The gateway is at `http://localhost:5080`; the bank is at `http://localhost:8787`.

```bash
curl http://localhost:5080/health/live
curl http://localhost:5080/health/ready
```

Compose enables migrations at startup for one local instance. Production deployments should run migrations as a separate deployment step.

## Local Development

Requirements: .NET 10 SDK and Docker.

```bash
dotnet tool restore
dotnet restore FicMart.PaymentGateway.slnx
docker compose -f docker/docker-compose.yaml up -d postgres bank-api payment-gateway-postgres
dotnet ef database update --project src/FicMart.PaymentGateway.Infrastructure

export FICMART_API_KEY='local-ficmart-api-key-at-least-32-characters'
export FicMartApi__ApiKey="$FICMART_API_KEY"
export Idempotency__FingerprintSecret='local-fingerprint-secret-at-least-32-characters'
export Idempotency__ProcessingLeaseSeconds=120
export ConnectionStrings__PaymentGateway='Host=localhost;Port=5433;Database=payment_gateway;Username=postgres;Password=postgres'
export Bank__BaseUrl='http://localhost:8787'

dotnet run --project src/FicMart.PaymentGateway.Api --urls http://localhost:5080
```

Set `FicMartApi__ApiKey`, `Idempotency__FingerprintSecret`, the gateway connection string, and bank URL using environment variables or user secrets. The service fails at startup when the gateway connection string is missing outside the development configuration. [`.env.example`](./.env.example) lists the keys. Never commit real secrets.

## API

Every `/api/v1` request requires `X-FicMart-Api-Key`. Authorize, capture, void, and refund also require a FicMart-owned `Idempotency-Key`.

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/v1/payments/authorize` | Authorize funds for an order. |
| `POST` | `/api/v1/payments/{paymentId}/capture` | Capture an authorized payment. |
| `POST` | `/api/v1/payments/{paymentId}/void` | Release an authorization before capture. |
| `POST` | `/api/v1/payments/{paymentId}/refund` | Fully refund a captured payment. |
| `POST` | `/api/v1/payments/{paymentId}/reconcile` | Retry recovery for an uncertain stored operation. |
| `GET` | `/api/v1/payments/{paymentId}` | Retrieve the gateway payment record. |
| `GET` | `/api/v1/payments/by-order/{orderId}` | Retrieve a payment using the FicMart order ID. |

```bash
curl -X POST http://localhost:5080/api/v1/payments/authorize \
  -H "Content-Type: application/json" \
  -H "X-FicMart-Api-Key: $FICMART_API_KEY" \
  -H "Idempotency-Key: order-1001-authorize" \
  -d '{"orderId":"order-1001","customerId":"customer-20","amountMinorUnits":2500,"currency":"USD","cardNumber":"4111111111111111","cvv":"123"}'
```

The sample card data is for the local mock bank only. PAN and CVV are sent to the bank but never persisted by the gateway.

Use the returned `paymentId` for later operations:

```bash
curl -X POST http://localhost:5080/api/v1/payments/{paymentId}/capture \
  -H "X-FicMart-Api-Key: $FICMART_API_KEY" \
  -H "Idempotency-Key: order-1001-capture"
```

When the bank times out, the gateway returns `202 Accepted` instead of guessing the result. FicMart should wait and query the payment before retrying. Retrying with the same idempotency key reuses the stored bank operation key and cannot intentionally create a second logical operation. A processing lease also lets the same request recover work abandoned by a crash or canceled request without treating an active duplicate as a new operation.

## Verify

```bash
dotnet build FicMart.PaymentGateway.slnx --no-restore
dotnet test FicMart.PaymentGateway.slnx --no-build
dotnet format FicMart.PaymentGateway.slnx --verify-no-changes --no-restore
dotnet ef migrations has-pending-model-changes \
  --project src/FicMart.PaymentGateway.Infrastructure \
  --startup-project src/FicMart.PaymentGateway.Infrastructure --no-build
```

Integration tests use Testcontainers and require Docker.

The current suite covers domain transitions, PostgreSQL constraints and migrations, duplicate requests, transient failures, abandoned and unknown-outcome recovery, authentication, full payment lifecycles, and stable replay after a capture-versus-void race.

## Documentation

- [Architecture](./ARCHITECTURE.md)
- [Data model](./DATA_MODEL.md)
- [API design](./API_DESIGN.md)
- [Security](./SECURITY.md)
- [Testing](./TESTING.md)
- [Performance](./PERFORMANCE.md)
- [Operations](./OPERATIONS.md)
- [Trade-offs](./TRADEOFFS.md)

This project is based on the [payment gateway exercise](https://github.com/benx421/payment-gateway).
