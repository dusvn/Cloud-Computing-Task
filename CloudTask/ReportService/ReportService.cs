using System.Collections.Concurrent;
using System.Fabric;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace ReportService
{
    /// <summary>
    /// Stateless sales report built from outbox messages sent by BookstoreService.
    /// Consumer is idempotent: a redelivered message just overwrites the same PurchaseId.
    /// </summary>
    internal sealed class ReportService : StatelessService, IReportService
    {
        private readonly ConcurrentDictionary<Guid, SaleEvent> _sales = new();

        public ReportService(StatelessServiceContext context)
            : base(context)
        { }

        public Task ConsumeSaleAsync(SaleEvent sale)
        {
            _sales[sale.PurchaseId] = sale;   // upsert by PurchaseId
            return Task.CompletedTask;
        }

        public Task<List<SaleEvent>> GetSalesReportAsync()
        {
            return Task.FromResult(_sales.Values.OrderBy(s => s.SoldAtUtc).ToList());
        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }
    }
}
