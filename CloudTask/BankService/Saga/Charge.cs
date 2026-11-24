namespace BankService.Saga
{
    public enum ChargeStatus
    {
        /// <summary>Money taken from the user.</summary>
        Charged = 0,
        /// <summary>Money given back.</summary>
        Refunded = 1,
        /// <summary>Refund came before Charge (or the charge was rejected): a late Charge must do nothing.</summary>
        Tombstone = 2
    }

    /// <summary>One saga's charge, keyed by txId in the reliable dictionary "charges".</summary>
    public class Charge
    {
        public Guid TxId { get; set; }
        public Guid UserId { get; set; }
        public double Amount { get; set; }
        public ChargeStatus Status { get; set; }
    }

    /// <summary>Bank outbox message: new balance of a user (absolute value, so a redelivery is harmless).</summary>
    public class BalanceChanged
    {
        public Guid UserId { get; set; }
        public double Balance { get; set; }
    }
}
