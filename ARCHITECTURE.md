# Architecture

> Engineering review draft. Verify the reasoning and rewrite disputed sections before presenting it as personal work.

The gateway is a modular monolith with three projects: Minimal API transport, payment domain rules, and infrastructure for PostgreSQL plus the mock-bank client. FicMart is the trusted caller; the bank and both databases are separate failure boundaries.

```text
FicMart -> Minimal API -> PaymentService -> PostgreSQL
                                  |
                                  +------> Mock Bank
```

Every money-moving request first records an operation and two idempotency keys: FicMart's key coordinates client duplicates, while a generated bank key coordinates retries downstream. Bank calls occur outside database transactions. A lost or ambiguous response leaves the operation retryable.

Capture and void completion use conditional PostgreSQL updates from `Authorized`, so concurrent successful responses cannot make the local payment both captured and voided. The bank remains the authority for actual money movement, and reconciliation exists because local and bank state can temporarily disagree.

Recovery is request-driven rather than a background worker. That keeps version one operable and testable, but recovery starts only when FicMart explicitly calls reconciliation.
