# Trade-offs

> Discussion draft generated from implemented decisions. Review and rewrite this in your own words before treating it as your personal design explanation.

## Modular Monolith

Chosen over microservices because one bounded workflow does not justify distributed deployment complexity. Separate projects retain dependency boundaries while one deployable keeps transactions and local development understandable.

## Integer Minor Units

Chosen because version one supports USD whole cents, making equality and amount checks exact. Multi-currency rounding would require a revised policy.

## Database Idempotency

PostgreSQL uniqueness coordinates duplicates across instances and restarts. In-memory locks would be faster but incorrect after either event. Records are retained indefinitely until an operational retention policy is agreed.

## Bounded Retry

Known bank `500` failures receive at most three attempts with the same bank key. Timeouts are not blindly retried in the same request because money may already have moved.

## Explicit Reconciliation

FicMart triggers recovery instead of a background scanner. This controls scope but does not recover stale operations autonomously. Authorization recovery requires the original request because persisting CVV would be an unacceptable trade-off.

## Full Operations

One full capture, void, and refund keeps version-one invariants precise. Partial operations would need cumulative amounts, multiple attempts, and over-capture and over-refund constraints.
