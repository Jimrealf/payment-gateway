# Data Model

> Engineering review draft.

`payments` is the current gateway receipt: opaque payment, order, and customer identifiers; amount in integer USD minor units; lifecycle status; timestamps. `order_id` is unique.

Four attempt tables preserve bank-operation state separately from payment lifecycle state. Each stores a stable bank idempotency UUID and only stores a bank reference after confirmed success. PAN and CVV are never columns.

`idempotency_records` is keyed by operation plus FicMart key and stores an HMAC request fingerprint, payment reference, and `Processing`, `Retryable`, or `Completed` state. `audit_records` stores append-only payment status changes. `reconciliation_records` stores each recovery finding.

Database checks constrain enum names and successful-attempt bank references. Unique indexes enforce one capture, void, and refund attempt per payment in version one. Migrations are forward-only; applied or shared migrations are not edited.
