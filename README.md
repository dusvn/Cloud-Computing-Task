# Cloud-Computing-Task

## System design

```mermaid
flowchart LR
    FE[React front] -->|purchase + idempotency key| C[Client<br/>stateless Web API]
    C --> V[Validator<br/>stateless]
    V -->|partition: purchase id| O[PurchaseOrchestrator<br/>stateful, 4 partitions]
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
    A([Purchase arrives with idempotency key]) --> B{Key already exists?}
    B -- yes --> B1([Return stored result<br/>no second purchase])
    B -- no --> C[Save state: Started]

    C --> D[Bookstore: ReserveBook]
    D --> E{Enough books?}
    E -- no --> E1[Save state: Failed] --> E2([409 - not enough books])
    E -- yes --> F[Save state: BookReserved]

    F --> G[Bank: ChargeAccount]
    G --> H{Enough money?}
    H -- no --> I[Save state: Compensating]
    I --> I1[Bank: RefundAccount]
    I1 --> I2[Bookstore: ReleaseBook<br/>held books returned]
    I2 --> I3[Save state: Compensated] --> I4([409 - purchase cancelled])

    H -- yes --> J[Save state: Confirming<br/>pivot: no way back]
    J --> K[Bookstore: ConfirmPurchase<br/>stock -= quantity + sale outbox]
    K --> L[Save state: Completed]
    L --> M([200 - order created, transactionId])
```

Every "Save state" is written to `saga_log` (PostgreSQL) and the reliable dictionary before the next
step, so if the orchestrator goes down it continues from the last saved state. Every call carries the
txId, so repeating it after a crash is safe.
