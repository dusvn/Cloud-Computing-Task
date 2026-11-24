using Common.Models;

namespace PurchaseOrchestrator.Saga.Commands
{
    /// <summary>Compensation of <see cref="ReserveBookCommand"/>: give the held books back (tombstone if nothing was reserved).</summary>
    internal sealed class ReleaseBookCommand : ISagaCommand
    {
        public string Name => "ReleaseBook";

        public Task<StepResult> ExecuteAsync(OrderRecord saga) =>
            Participants.Bookstore(saga.BookId).ReleaseAsync(saga.TransactionId, saga.BookId);
    }
}
