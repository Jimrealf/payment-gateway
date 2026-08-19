# API Design

> Engineering review draft.

All versioned routes require `X-FicMart-Api-Key`. Authorize, capture, void, and refund additionally require `Idempotency-Key` with a maximum length of 128 characters.

Authorization accepts order ID, customer ID, positive integer minor units, `USD`, PAN, and CVV. Later operations are full-amount operations identified by payment ID. Queries return gateway-owned safe fields and `safeToShip`; they omit customer, card, and bank references.

Known success returns `201` for authorization and `200` later. In-progress or unknown operations return `202`. Validation is `400`, authentication `401`, missing payment `404`, invalid lifecycle or key conflict `409`, bank decline `402`, and exhausted transient failures `503`.

An active duplicate remains `202`. After the configured processing lease expires, the original request may atomically reclaim abandoned work and retry with the same downstream bank key. If the bank completed an operation but a competing lifecycle transition won locally, the initial response and later replay remain a `409 payment_state_conflict`.

Reconciliation replays stored non-card operations with the original downstream key. Unknown authorization reports `operator_review_required` because replay needs FicMart's original CVV.
