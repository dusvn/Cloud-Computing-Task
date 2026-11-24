using Common.Models;

namespace PurchaseOrchestrator.Saga.Commands
{
    /// <summary>Compensation of <see cref="ChargeAccountCommand"/>: give the money back (tombstone if nothing was charged).</summary>
    internal sealed class RefundAccountCommand : ISagaCommand
    {
        public string Name => "RefundAccount";

        public Task<StepResult> ExecuteAsync(OrderRecord saga) =>
            Participants.Bank(saga.UserId).RefundAsync(saga.TransactionId, saga.UserId);
    }
}
