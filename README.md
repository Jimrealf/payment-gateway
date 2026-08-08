# FicMart Payment Gateway

An in-progress payment gateway for FicMart, a fictional e-commerce platform. I am building it in C# on .NET 10 LTS to learn how payment systems handle state transitions, duplicate requests, unreliable external services, and uncertain transaction outcomes.

The gateway will sit between FicMart and a supplied mock bank API. FicMart will send payment requests to the gateway, and the gateway will keep its own payment records while coordinating each operation with the bank.

## Status

The project is in its initial build stage. The repository and mock bank are set up, the bank contract has been inspected and tested locally, and .NET 10 LTS has been selected for the gateway.

The C# gateway has not been scaffolded yet. The next step is to finish the minimum API, persistence, and testing decisions needed for the first authorization flow.

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
docker compose -f docker/docker-compose.yaml exec bank-api go test ./...
```

## Mock Bank Behavior

The mock bank deliberately introduces latency and random server errors. It also enforces operation ordering, uses integer cents for amounts, and requires an `Idempotency-Key` header for every mutating request.

Two implementation details matter for the gateway:

- The running authorization endpoint accepts requests without the expiry fields marked as required by Swagger.
- The bank caches successful responses by idempotency key and request path, but it does not compare request payloads or guarantee serialization of concurrent first requests.

The gateway will not rely on the bank's idempotency behavior as its only duplicate-request protection.

## Repository Structure

```text
bank/                 supplied mock bank API and tests
docker/               local bank and PostgreSQL environment
README.md              current project status and setup
```

The gateway solution and its tests will be added after the remaining Stage 0 decisions are complete.

## Project Origin

This project is based on the [payment gateway exercise](https://github.com/benx421/payment-gateway) from the [Backend Engineer Path](https://github.com/benx421/backend-engineer-path).
