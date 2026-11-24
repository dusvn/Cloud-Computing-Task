using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
    /// <summary>Saga orchestrator. Partitioned by purchase id (= idempotency key), see PartitionHelper.ForPurchase.</summary>
    public interface IPurchaseOrchestrator : IService
    {
        /// <summary>
        /// Starts the saga and returns at once (202); the saga runs in the background.
        /// Idempotent: a repeated key does not start a second saga, it returns the existing purchase.
        /// </summary>
        Task<PurchaseResponse> StartPurchaseAsync(Purchase p);

        /// <summary>Current state of the purchase, or null when the id is unknown.</summary>
        Task<OrderRecord?> GetPurchaseAsync(Guid purchaseId);

        /// <summary>Cancel: 202 and compensation before the pivot, 409 after it, 404 when unknown.</summary>
        Task<PurchaseResponse> CancelPurchaseAsync(Guid purchaseId);

        /// <summary>Purchases of one user stored in THIS partition.</summary>
        Task<List<OrderRecord>> GetOrders(Guid userId);
    }
}
