using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
    public interface IReportService : IService
    {
        /// <summary>Idempotent consumer of outbox messages: upsert by PurchaseId.</summary>
        Task ConsumeSaleAsync(SaleEvent sale);

        Task<List<SaleEvent>> GetSalesReportAsync();
    }
}
