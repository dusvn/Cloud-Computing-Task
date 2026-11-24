namespace Common.Models
{
    /// <summary>
    /// States of the purchase saga.
    /// Happy path:   Started → BookReserved → Confirming → Completed
    /// Compensation: Compensating → Compensated
    /// Failed: the book could not be reserved (nothing to undo).
    /// Confirming is the pivot: from there on the saga only goes forward (retry, never compensate).
    /// </summary>
    public enum SagaState
    {
        Started = 0,
        BookReserved = 1,
        Confirming = 2,
        Completed = 3,
        Compensating = 4,
        Compensated = 5,
        Failed = 6
    }

    /// <summary>
    /// One purchase saga, stored under its idempotency key in the orchestrator's reliable dictionary "sagas"
    /// (the replicated saga log used for recovery) and mirrored to the Postgres table public.orders.
    /// Holds everything needed to resume the saga on another replica.
    /// </summary>
    public class OrderRecord
    {
        public Guid IdempotencyKey { get; set; }
        /// <summary>Saga / transaction id sent with every command; participants are idempotent by it.</summary>
        public Guid TransactionId { get; set; }
        public Guid UserId { get; set; }
        public Guid BookId { get; set; }
        public string BookName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Quantity { get; set; }
        /// <summary>Price × quantity, known after the book is reserved.</summary>
        public double Amount { get; set; }
        public SagaState Status { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>Failed attempts of the current step (reset on every state change).</summary>
        public int Attempts { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public bool IsFinished => Status is SagaState.Completed or SagaState.Compensated or SagaState.Failed;
    }
}
