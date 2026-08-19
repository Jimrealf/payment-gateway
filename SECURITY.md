# Security

> Engineering review draft. This learning project is not a production card-processing certification.

FicMart authenticates with a configured API key. Comparison uses fixed-time hash comparison, and health endpoints remain public for orchestration. TLS termination is expected in front of deployed instances.

PAN and CVV cross the gateway only in memory for authorization and are never persisted, logged, audited, or returned. The request fingerprint includes PAN through HMAC-SHA-256; CVV is excluded entirely. Secrets belong in environment-backed secret storage.

Correlation IDs accept only a bounded safe character set. Client errors use stable messages and the global handler suppresses stack traces and raw bank errors. Audit rows contain safe IDs and state changes only.

Remaining production work includes key rotation, rate limiting, network policy, managed secrets, formal PCI scope review, telemetry export, audit retention, and separate operator authorization.
