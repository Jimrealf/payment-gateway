# Mock Bank Contract Audit

## Status

- Audited: 2026-08-08
- Scope: upstream bank API, routes, handlers, idempotency middleware, chaos middleware, migrations, and integration tests
- Purpose: record external facts and implementation drift before designing the gateway

This document describes the supplied bank. It does not prescribe our gateway API or architecture.

## Sources Reviewed

1. `README.md`
2. `bank/api/openapi.yaml`
3. `bank/internal/handlers/routes.go`
4. `bank/internal/handlers/authorization.go`
5. `bank/internal/handlers/capture.go`
6. `bank/internal/handlers/void.go`
7. `bank/internal/handlers/refund.go`
8. `bank/internal/middleware/idempotency.go`
9. `bank/internal/middleware/idempotency_test.go`
10. `bank/internal/middleware/chaos.go`
11. `bank/internal/service/authorization.go`
12. `bank/internal/service/errors.go`
13. `bank/internal/db/migrations/000001_init.up.sql`
14. `bank/tests/api_test.go`
15. `bank/tests/setup_test.go`
16. `docker/docker-compose.yaml`

## Runtime Contract

- Local bank base URL: `http://localhost:8787`
- Swagger UI: `http://localhost:8787/docs`
- Health endpoint: `GET /health`
- Bank database: PostgreSQL 16 in the supplied Compose stack
- Default authorization lifetime: 168 hours (7 days)
- Default injected failure rate: 5%
- Default injected latency: 100-2000 ms
- Chaos injection applies to API operations, including lookup requests; `/health` and `/docs` are excluded.
- Chaos settings are configurable for deterministic testing.

## Endpoint Summary

| Operation | Method and path | Idempotency key | Lookup available |
| --- | --- | --- | --- |
| Authorize | `POST /api/v1/authorizations` | Required | `GET /api/v1/authorizations/{authorizationId}` |
| Capture | `POST /api/v1/captures` | Required | `GET /api/v1/captures/{captureId}` |
| Void | `POST /api/v1/voids` | Required | No void lookup endpoint is declared |
| Refund | `POST /api/v1/refunds` | Required | `GET /api/v1/refunds/{refundId}` |

All mutating endpoints use the `Idempotency-Key` request header. The OpenAPI limit is 1-255 characters.

## Authorization

### Declared request

`POST /api/v1/authorizations`

OpenAPI declares these fields as required:

- `card_number`: 13-19 digits, Luhn validated
- `cvv`: 3-4 digits
- `expiry_month`: integer from 1 through 12
- `expiry_year`: integer from 2024 through 2099
- `amount`: positive 64-bit integer in cents

### Successful response

HTTP `200` with:

- `authorization_id`: bank-generated ID with `auth_` prefix
- `status`: `approved`
- `amount`: integer cents
- `currency`: `USD`
- `expires_at`
- `created_at`

### Declared errors

- HTTP `400`: validation or business-rule error
- HTTP `402`: insufficient funds
- HTTP `500`: internal or injected transient error

Relevant error codes include:

- `invalid_card`
- `invalid_cvv`
- `invalid_amount`
- `card_expired`
- `insufficient_funds`
- `missing_idempotency_key`
- `internal_error`

### Lookup

`GET /api/v1/authorizations/{authorizationId}` returns HTTP `200` or `404`.

The lookup requires the bank-generated authorization ID. It cannot be called using our gateway payment ID or the bank idempotency key.

## Capture

`POST /api/v1/captures`

Request:

- `authorization_id`
- `amount` in cents, which must match the authorization
- `Idempotency-Key` header

Success: HTTP `200` with `capture_id`, `authorization_id`, `status`, `amount`, `currency`, and `captured_at`.

Lookup: `GET /api/v1/captures/{captureId}`.

Relevant errors include authorization not found, authorization expired, authorization already used, amount mismatch, and internal error.

## Void

`POST /api/v1/voids`

Request:

- `authorization_id`
- `Idempotency-Key` header

Success: HTTP `200` with `void_id`, `authorization_id`, `status`, and `voided_at`.

No GET endpoint for a void resource is declared in the OpenAPI contract.

Relevant errors include authorization not found, authorization expired, authorization already used, already captured, already voided, and internal error.

## Refund

`POST /api/v1/refunds`

Request:

- `capture_id`
- `amount` in cents, which must match the capture
- `Idempotency-Key` header

Success: HTTP `200` with `refund_id`, `capture_id`, `status`, `amount`, `currency`, and `refunded_at`.

Lookup: `GET /api/v1/refunds/{refundId}`.

Relevant errors include capture not found, already refunded, amount mismatch, and internal error.

## Bank Idempotency Semantics

Observed implementation behavior:

1. Cache identity is `(Idempotency-Key, request path)`.
2. The request body is not stored or compared.
3. The same key may be reused on a different endpoint because the path is part of the identity.
4. Only `2xx` responses are cached.
5. `4xx` and `5xx` responses are not cached.
6. A replayed response includes `X-Idempotent-Replayed: true`.
7. A cache read failure fails open and allows the bank operation to execute.
8. A cache write failure does not change a successful bank response.
9. Cache storage happens after the bank handler has completed.
10. The database insert uses conflict-ignore behavior on `(key, request_path)`.

### Consequences for our gateway

These are external constraints, not gateway decisions:

- If a successful response is lost after the bank caches it, retrying the same path with the same key can recover the original response.
- Reusing the same key and path with a different payload can replay the previous successful response; the bank does not detect the payload mismatch.
- A `4xx` or `5xx` response cannot be recovered from the bank's idempotency cache because it is not stored.
- A successful money operation followed by bank idempotency-cache write failure creates a recovery gap.
- Concurrent requests with the same key can both pass the initial cache lookup before either response is stored; the middleware does not visibly serialize them.
- Our gateway must not assume the bank's idempotency layer is a complete substitute for gateway-level idempotency and recovery records.

## Concurrency and State Enforcement

The bank uses database transactions and row-level account locking for authorization. It also has a partial unique index intended to prevent duplicate capture, void, or refund records for the same reference and operation type.

The upstream integration tests verify:

- Only one of ten concurrent capture attempts succeeds.
- Capture after an authorization has already been used fails.
- Void after capture fails.
- Replaying a successful request with the same bank idempotency key returns the cached response.

## Contract Drift and Open Evidence Questions

### Authorization expiry fields

OpenAPI requires `expiry_month` and `expiry_year`, but the integration-test authorization helper sends only card number, CVV, and amount. The authorization handler passes only card number, CVV, and amount to the service, and the service validates expiry from the bank's stored account record.

Live verification on 2026-08-08 confirmed that the bank accepts an authorization containing only card number, CVV, and amount. It returned `200 OK` with an approved authorization. The declared OpenAPI request and the running implementation are therefore inconsistent.

Stage 0 conclusion: treat the running implementation as the integration behavior while keeping the mismatch visible. We still need to decide together whether our bank client should send the documented expiry fields or the smaller payload accepted by the implementation.

### Idempotency payload mismatch

The bank cache does not fingerprint the request body. Live verification on 2026-08-08 confirmed that replaying the same authorization payload with the same key returned the original response and `X-Idempotent-Replayed: true`. We have not yet sent a different payload under that key because doing so is unnecessary for the first scaffold decision; the source shows that such a request would be matched by key and path rather than payload.

### Concurrent same-key authorization

The existing tests cover concurrent captures with different keys, but do not prove that concurrent authorization requests using the same bank idempotency key execute once. We should add a gateway-side test scenario for this risk rather than assuming bank serialization.

## Recovery Implication From Our Earlier Discussion

Persisting our authorization intent before calling the bank remains a candidate gateway approach.

If the gateway times out before receiving `authorization_id`, it cannot use the bank's GET endpoint. Its only available bank correlation value is the idempotency key used for the POST request. Replaying the same POST with the same payload and key may recover a cached successful response, subject to the limitations above.
