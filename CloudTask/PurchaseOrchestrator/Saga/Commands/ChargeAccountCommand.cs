using Common.Models;

namespace PurchaseOrchestrator.Saga.Commands
{
    /// <summary>Step 2: take the money from the user's account at the Bank.</summary>
    internal sealed class ChargeAccountCommand : ISagaCommand
    {
        public string Name => "ChargeAccount";

        public Task<StepResult> ExecuteAsync(OrderRecord saga) =>
            Participants.Bank(saga.UserId).ChargeAsync(saga.TransactionId, saga.UserId, saga.Amount);
    }
}
