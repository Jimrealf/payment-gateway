# Performance

> Engineering review draft. Refresh measurements on the target machine before setting an SLO.

The write path performs a small number of indexed PostgreSQL operations and one external bank call. Bank latency dominates normal command latency. Payment and idempotency lookups use primary or unique indexes.

Contention is concentrated at database uniqueness constraints and conditional state updates. No transaction remains open while waiting for the bank. Transient retries are bounded at three with short increasing delays.

Version one has no cache, queue, or batch worker because current requirements do not justify them. Before production, measure p50/p95/p99 latency, lock time, reconciliation backlog, retry amplification, and throughput under duplicate and capture-versus-void contention.

Local verification on 2026-08-19 ran 42 unit and PostgreSQL integration tests in about 26 seconds, including container startup. This is a reproducibility baseline, not an API latency SLO; the service records bank-attempt latency so deployed measurements can establish real targets.
