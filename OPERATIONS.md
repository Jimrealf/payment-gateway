# Operations

> Engineering review draft.

`/health/live` proves the process serves HTTP. `/health/ready` additionally checks the gateway database. The root `/health` remains for compatibility.

Docker Compose may apply migrations at startup because it runs one local instance. Production should apply migrations once in a deployment job, then start replicas with `Database__ApplyMigrations=false`.

Production must provide `ConnectionStrings__PaymentGateway`; the application fails fast when it is absent. `Idempotency__ProcessingLeaseSeconds` defaults to 120 seconds and must remain longer than the maximum expected bank retry window. After that lease, another request may reclaim abandoned work using the original bank idempotency key.

For an uncertain payment, query the receipt and call reconciliation. Capture, void, and refund can replay stored non-sensitive identifiers and the original bank key. Unknown authorization requires FicMart to retry the original request or operator review because CVV is never stored.

Monitor command and reconciliation outcomes, readiness, HTTP 5xx, bank timeouts, retries, and database saturation. Audit and reconciliation retention remains an operational decision.
