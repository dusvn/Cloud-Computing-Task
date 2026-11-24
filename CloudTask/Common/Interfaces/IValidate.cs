using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
    public interface IValidate : IService
    {
        Task<PurchaseResponse> TransferData(Purchase p);

        /// <summary>Orders of one user (newest first), read from the orchestrator's reliable collection.</summary>
        Task<List<OrderRecord>> GetOrders(Guid userId);
    }
}
