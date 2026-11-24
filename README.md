# Cloud-Computing-Task

## System design

```mermaid
flowchart LR
    FE[React front] -->|POST /api/purchases + Idempotency-Key<br/>GET / DELETE /api/purchases/id| C[Client<br/>stateless Web API]
    C --> V[Validator<br/>stateless]
    V -->|partition: purchase id| O[PurchaseOrchestrator<br/>stateful, 4 partitions]
    V -.->|fan-out: list books| B
    V -.->|fan-out: list clients| K
    O -->|Reserve / Confirm / Release<br/>partition: book id| B[BookstoreService<br/>stateful, 4 partitions]
    O -->|Charge / Refund<br/>partition: user id| K[BankService<br/>stateful, 4 partitions]
    O -->|saga_log, orders| DB[(PostgreSQL)]
    B -->|outbox: stock| DB
    B -->|outbox: sale| R[ReportService<br/>stateless]
    K -->|outbox: balance| DB
```

## Saga pattern

```mermaid
flowchart TD
    A([POST /api/purchases<br/>with Idempotency-Key]) --> B{Key already exists?}
    B -- yes --> B1([202 + Location<br/>existing purchase, no second saga])
    B -- no --> C[Save state: Started]
    C --> C1([202 + Location: /api/purchases/id<br/>saga continues in the background])

    C --> D[Bookstore: ReserveBook]
    D --> E{Enough books?}
    E -- no --> E1[Save state: Failed]
    E -- yes --> F[Save state: BookReserved]

    F --> G[Bank: ChargeAccount]
    G --> H{Enough money?}
    H -- no --> I[Save state: Compensating]
    I --> I1[Bank: RefundAccount]
    I1 --> I2[Bookstore: ReleaseBook<br/>held books returned]
    I2 --> I3[Save state: Compensated]

    H -- yes --> J[Save state: Confirming<br/>pivot: no way back]
    J --> K[Bookstore: ConfirmPurchase<br/>stock -= quantity + sale outbox]
    K --> L[Save state: Completed]

    X([DELETE /api/purchases/id]) --> Y{Before pivot?<br/>Started / BookReserved}
    Y -- yes --> Y1([202]) --> I
    Y -- no --> Y2([409 - cannot cancel])
```

The front follows the purchase with `GET /api/purchases/{id}` until the state is `Completed`,
`Compensated` or `Failed`.

Every "Save state" is written to `saga_log` (PostgreSQL) and the reliable dictionary before the next
step, so if the orchestrator goes down it continues from the last saved state. Every call carries the
txId, so repeating it after a crash is safe.
