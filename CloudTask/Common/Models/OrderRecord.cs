namespace Common.Models
{
    public enum OrderStatus
    {
        Pending = 0,
        Completed = 1,
        Failed = 2
    }

    /// <summary>
    /// One order, stored under its idempotency key in the orchestrator's reliable dictionary
    /// and mirrored to the Postgres table public.orders.
    /// </summary>
    public class OrderRecord
    {
        public Guid IdempotencyKey { get; set; }
        /// <summary>Id of the distributed transaction (also written to the sales report as PurchaseId).</summary>
        public Guid TransactionId { get; set; }
        public Guid UserId { get; set; }
        public Guid BookId { get; set; }
        public string BookName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double Amount { get; set; }
        public OrderStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}
