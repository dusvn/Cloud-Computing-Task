using Common.Models;

namespace PurchaseOrchestrator.Saga.Commands
{
    /// <summary>
    /// Step 3 (pivot): the Bookstore removes the books from stock and writes the sale to its outbox in one transaction.
    /// After the saga reached this step it is only retried, never compensated.
    /// </summary>
    internal sealed class ConfirmPurchaseCommand : ISagaCommand
    {
        public string Name => "ConfirmPurchase";

        public Task<StepResult> ExecuteAsync(OrderRecord saga) =>
            Participants.Bookstore(saga.BookId).ConfirmAsync(saga.TransactionId);
    }
}
