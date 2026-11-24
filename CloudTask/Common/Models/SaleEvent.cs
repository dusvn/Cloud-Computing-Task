namespace Common.Models
{
    /// <summary>
    /// Outbox message describing one confirmed sale. Enqueued by the Bookstore in the same transaction
    /// that removes the books from stock, delivered at-least-once to Postgres (stock) and ReportService.
    /// </summary>
    public class SaleEvent
    {
        public Guid PurchaseId { get; set; }
        public Guid BookId { get; set; }
        public Guid UserId { get; set; }
        public string BookName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double Amount { get; set; }
        public string Email { get; set; } = string.Empty;
        /// <summary>Stock of the book right after this sale (absolute value, so a redelivery is harmless).</summary>
        public int RemainingStock { get; set; }
        public DateTime SoldAtUtc { get; set; }
    }
}
