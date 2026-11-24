using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
    /// <summary>Validation and routing layer between the HTTP API (Client) and the stateful services.</summary>
    public interface IValidate : IService
    {
        /// <summary>Validates the purchase (400) and starts it in the orchestrator partition of its purchase id (202).</summary>
        Task<PurchaseResponse> TransferData(Purchase p);

        Task<OrderRecord?> GetPurchase(Guid purchaseId);

        Task<PurchaseResponse> CancelPurchase(Guid purchaseId);

        /// <summary>Orders of one user (newest first), fan-out over all orchestrator partitions.</summary>
        Task<List<OrderRecord>> GetOrders(Guid userId);

        /// <summary>Books with available stock, fan-out over all Bookstore partitions.</summary>
        Task<List<Book>> ListAvailableItems();

        /// <summary>Users with their balance, fan-out over all Bank partitions.</summary>
        Task<List<User>> ListClients();
    }
}
