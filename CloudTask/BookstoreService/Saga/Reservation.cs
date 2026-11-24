namespace BookstoreService.Saga
{
    public enum ReservationStatus
    {
        /// <summary>Books are held for the saga, stock not removed yet.</summary>
        Reserved = 0,
        /// <summary>Sale done: stock removed and sale put in the outbox.</summary>
        Confirmed = 1,
        /// <summary>Held books given back (or tombstone: Release came before Reserve).</summary>
        Released = 2
    }

    /// <summary>One saga's reservation, keyed by txId in the reliable dictionary "reservations".</summary>
    public class Reservation
    {
        public Guid TxId { get; set; }
        public Guid BookId { get; set; }
        public Guid UserId { get; set; }
        public string BookName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double Amount { get; set; }
        public ReservationStatus Status { get; set; }
    }
}
