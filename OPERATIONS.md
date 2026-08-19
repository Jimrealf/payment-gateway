# Operations

> Engineering review draft.

`/health/live` proves the process serves HTTP. `/health/ready` additionally checks the gateway database. The root `/health` remains for compatibility.

Docker Compose may apply migrations at startup because it runs one local instance. Production should apply migrations once in a deployment job, then start replicas with `Database__ApplyMigrations=false`.

For an uncertain payment, query the receipt and call reconciliation. Capture, void, and refund can replay stored non-sensitive identifiers and the original bank key. Unknown authorization requires FicMart to retry the original request or operator review because CVV is never stored.

Monitor command and reconciliation outcomes, readiness, HTTP 5xx, bank timeouts, retries, and database saturation. Audit and reconciliation retention remains an operational decision.
