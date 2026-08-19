# Testing

> Engineering review draft.

Unit tests protect reachable domain transitions, identifier validation, and money invariants. Integration tests use `WebApplicationFactory`, a typed scripted bank, and real PostgreSQL through Testcontainers.

Critical scenarios cover idempotent replay, concurrent duplicates, bounded retry, abandoned and unknown recovery, full lifecycle operations, invalid transitions, capture-versus-void contention and replay, safe queries, reconciliation, authentication, and correlation IDs.

Tests avoid impossible domain states, duplicate validator permutations, and flaky wall-clock latency assertions. Bank client contract tests cover HTTP mapping. Docker must run for PostgreSQL integration tests.
