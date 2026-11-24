using Common.Models;

namespace PurchaseOrchestrator.Saga.Commands
{
    /// <summary>Step 1: hold the books at the Bookstore. On success the saga learns the amount to charge.</summary>
    internal sealed class ReserveBookCommand : ISagaCommand
    {
        public string Name => "ReserveBook";

        public async Task<StepResult> ExecuteAsync(OrderRecord saga)
        {
            var purchase = new Purchase
            {
                UserId = saga.UserId,
                BookId = saga.BookId,
                BookName = saga.BookName,
                Quantity = saga.Quantity,
                Email = saga.Email
            };
            StepResult result = await Participants.Bookstore(saga.BookId).ReserveAsync(saga.TransactionId, purchase);
            if (result.Succeeded) saga.Amount = result.Amount;
            return result;
        }
    }
}
