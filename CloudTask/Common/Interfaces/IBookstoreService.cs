using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
    /// <summary>Saga participant. Every command is idempotent by txId: repeating it returns the first outcome.</summary>
    public interface IBookstoreService : IService
    {
        /// <summary>Holds the books for txId (stock is not removed yet). Ok carries the amount; Fail = unknown book / not enough stock.</summary>
        Task<StepResult> ReserveAsync(Guid txId, Purchase purchase);

        /// <summary>Pivot: Reserved → Confirmed, stock -= quantity and the sale is put in the outbox, all in one transaction.</summary>
        Task<StepResult> ConfirmAsync(Guid txId);

        /// <summary>Compensation: gives the held books back. Arriving before Reserve, it leaves a tombstone so a late Reserve does nothing.</summary>
        Task<StepResult> ReleaseAsync(Guid txId, Guid bookId);
    }
}
