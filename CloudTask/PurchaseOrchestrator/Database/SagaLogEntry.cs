using Common.Models;

namespace PurchaseOrchestrator.Database
{
    /// <summary>
    /// One row of public.saga_log: a full snapshot of a saga at one moment (append-only).
    /// The newest row per idempotency key is enough to resume the saga after the orchestrator goes down.
    /// </summary>
    internal sealed class SagaLogEntry
    {
        public long Id { get; set; }
        public Guid IdempotencyKey { get; set; }
        public Guid TransactionId { get; set; }
        public Guid UserId { get; set; }
        public Guid BookId { get; set; }
        public string BookName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double Amount { get; set; }
        public SagaState State { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Attempts { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LoggedUtc { get; set; }

        public static SagaLogEntry From(OrderRecord s) => new()
        {
            IdempotencyKey = s.IdempotencyKey,
            TransactionId = s.TransactionId,
            UserId = s.UserId,
            BookId = s.BookId,
            BookName = s.BookName,
            Email = s.Email,
            Quantity = s.Quantity,
            Amount = s.Amount,
            State = s.Status,
            Message = s.Message,
            Attempts = s.Attempts,
            CreatedUtc = s.CreatedUtc,
            LoggedUtc = DateTime.UtcNow
        };

        public OrderRecord ToSaga() => new()
        {
            IdempotencyKey = IdempotencyKey,
            TransactionId = TransactionId,
            UserId = UserId,
            BookId = BookId,
            BookName = BookName,
            Email = Email,
            Quantity = Quantity,
            Amount = Amount,
            Status = State,
            Message = Message,
            Attempts = Attempts,
            CreatedUtc = CreatedUtc,
            UpdatedUtc = LoggedUtc
        };
    }
}
