using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Interfaces
{
    public interface IPurchaseOrchestrator : IService
    {
        /// <summary>
        /// Idempotent: the first call with a given <see cref="Purchase.IdempotencyKey"/> runs the purchase,
        /// every repeated call returns the stored outcome instead of buying again.
        /// </summary>
        Task<PurchaseResponse> TransferDataAndPurchase(Purchase p);

        Task<List<OrderRecord>> GetOrders(Guid userId);
    }
}
