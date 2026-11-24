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

What happens when a purchase comes in. Before every command the orchestrator saves the saga state
(saga_log + reliable dictionary), so after a crash it continues from the last saved state.

```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant O as PurchaseOrchestrator
    participant S as Saga state<br/>(saga_log + sagas)
    participant B as BookstoreService
    participant K as BankService

    U->>O: purchase (idempotency key)
    O->>S: key already exists?
    alt key exists (repeated request)
        O-->>U: stored result (200 / 202 / 409), no second purchase
    else first request
        O->>S: save Started
        O->>B: ReserveBook(txId)
        alt no stock / unknown book
            B-->>O: rejected
            O->>S: save Failed
            O-->>U: 409 rejected
        else books held
            B-->>O: ok, amount
            O->>S: save BookReserved
            O->>K: ChargeAccount(txId, amount)
            alt not enough money
                K-->>O: rejected
                O->>S: save Compensating
                O->>K: RefundAccount(txId)
                O->>B: ReleaseBook(txId) — held books returned
                O->>S: save Compensated
                O-->>U: 409 purchase cancelled
            else charged
                Note over K: balance -= amount<br/>+ balance outbox (one transaction)
                K-->>O: ok
                O->>S: save Confirming (pivot: only forward from here)
                O->>B: ConfirmPurchase(txId)
                Note over B: stock -= quantity<br/>+ sale outbox (one transaction)
                B-->>O: ok
                O->>S: save Completed
                O-->>U: 200 order created (transactionId)
            end
        end
    end
```

On a timeout or failover the same command is retried (it is idempotent by txId); before the pivot,
after 5 failed attempts the saga compensates instead.
