# FicMart Payment Gateway

An in-progress payment gateway for FicMart, a fictional e-commerce platform. I am building it in C# on .NET 10 LTS to learn how payment systems handle state transitions, duplicate requests, unreliable external services, and uncertain transaction outcomes.

The gateway will sit between FicMart and a supplied mock bank API. FicMart will send payment requests to the gateway, and the gateway will keep its own payment records while coordinating each operation with the bank.

## Status

The project is in its initial build stage. The repository and mock bank are set up, the bank contract has been inspected locally, and the .NET 10 solution now builds and runs.

The gateway currently exposes only a health endpoint. The next implementation slice will define and build the first authorization flow.

## Current Scope

The completed gateway will support the payment lifecycle used by FicMart:

1. Authorize funds when an order is placed.
2. Capture an authorization when the order ships.
3. Void an authorization when an order is cancelled before capture.
4. Refund a captured payment after a return.

The first implementation slice will focus on authorization. Later operations will be added incrementally after their behavior and failure cases have been discussed.

## Requirements

- Docker 20.10 or newer
- Docker Compose 2.0 or newer
- .NET 10 SDK for the gateway implementation

## Run the Mock Bank

Start the bank API and its PostgreSQL database from the repository root:

```bash
docker compose -f docker/docker-compose.yaml up -d --build
```

Check that the bank is healthy:

```bash
curl http://localhost:8787/health
```

The Swagger UI is available at <http://localhost:8787/docs>.

Stop the bank:

```bash
docker compose -f docker/docker-compose.yaml down
```

Run the supplied bank tests while the containers are running:

```bash
docker compose -f docker/docker-compose.yaml exec bank-api sh -c \
  'go test -v $(go list ./... | grep -v /tests) && go test -v -count=1 -p 1 ./tests/...'
```

## Build the Gateway

Restore dependencies and build the solution:

```bash
dotnet restore FicMart.PaymentGateway.slnx
dotnet build FicMart.PaymentGateway.slnx --no-restore
```

Run the tests:

```bash
dotnet test FicMart.PaymentGateway.slnx --no-build
```

Check formatting without changing files:

```bash
dotnet format FicMart.PaymentGateway.slnx --verify-no-changes
```

Run the gateway locally:

```bash
dotnet run --project src/FicMart.PaymentGateway.Api --urls http://localhost:5080
```

The health endpoint is available at <http://localhost:5080/health>.

Local configuration keys are shown in [`.env.example`](./.env.example). Supply them as environment variables or through .NET user secrets; do not commit real credentials.

## Mock Bank Behavior

The mock bank deliberately introduces latency and random server errors. It also enforces operation ordering, uses integer cents for amounts, and requires an `Idempotency-Key` header for every mutating request.

Two implementation details matter for the gateway:

- The running authorization endpoint accepts requests without the expiry fields marked as required by Swagger.
- The bank caches successful responses by idempotency key and request path, but it does not compare request payloads or guarantee serialization of concurrent first requests.

The gateway will not rely on the bank's idempotency behavior as its only duplicate-request protection.

## Repository Structure

```text
src/
  FicMart.PaymentGateway.Api/             Minimal API host
  FicMart.PaymentGateway.Domain/          payment rules and domain types
  FicMart.PaymentGateway.Infrastructure/  EF Core and external integrations
tests/
  FicMart.PaymentGateway.IntegrationTests/ HTTP and PostgreSQL integration tests
bank/                                      supplied mock bank API and tests
docker/                                    local mock bank environment
```

## Project Origin

This project is based on the [payment gateway exercise](https://github.com/benx421/payment-gateway) from the [Backend Engineer Path](https://github.com/benx421/backend-engineer-path).
