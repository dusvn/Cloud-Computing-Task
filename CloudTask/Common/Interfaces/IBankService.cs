using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
    /// <summary>Saga participant. Every command is idempotent by txId: repeating it returns the first outcome.</summary>
    public interface IBankService : IService
    {
        /// <summary>Takes the amount from the user's balance. Fail = unknown user / not enough money / already refunded (tombstone).</summary>
        Task<StepResult> ChargeAsync(Guid txId, Guid userId, double amount);

        /// <summary>Compensation: returns the money. Arriving before Charge, it leaves a tombstone so a late Charge does nothing.</summary>
        Task<StepResult> RefundAsync(Guid txId, Guid userId);
    }
}
