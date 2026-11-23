namespace Common.Models
{
    /// <summary>Outbox message describing one completed sale.</summary>
    public class SaleEvent
    {
        public Guid PurchaseId { get; set; }
        public string BookName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double Amount { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateTime SoldAtUtc { get; set; }
    }
}
