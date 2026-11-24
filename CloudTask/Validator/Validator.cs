using System.Fabric;
using Common;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace Validator
{
    /// <summary>
    /// Stateless layer between the HTTP API and the stateful services: validates purchases (400),
    /// routes single-purchase calls to the orchestrator partition of the purchase id, and does the
    /// fan-out reads over all partitions of a service.
    /// </summary>
    internal sealed class Validator : StatelessService, IValidate
    {
        private static readonly Uri OrchestratorUri = new("fabric:/CloudTask/PurchaseOrchestrator");
        private static readonly Uri BookstoreUri = new("fabric:/CloudTask/BookstoreService");
        private static readonly Uri BankUri = new("fabric:/CloudTask/BankService");

        private readonly PurchaseValidator _purchaseValidator = new();

        public Validator(StatelessServiceContext context)
            : base(context)
        { }

        public async Task<PurchaseResponse> TransferData(Purchase p)
        {
            PurchaseResponse validation = _purchaseValidator.Validate(p);
            if (!validation.IsAccepted) return validation;   // 400 with reason, never retried

            return await Orchestrator(p.IdempotencyKey).StartPurchaseAsync(p);
        }

        public Task<OrderRecord?> GetPurchase(Guid purchaseId) =>
            Orchestrator(purchaseId).GetPurchaseAsync(purchaseId);

        public Task<PurchaseResponse> CancelPurchase(Guid purchaseId) =>
            Orchestrator(purchaseId).CancelPurchaseAsync(purchaseId);

        public async Task<List<OrderRecord>> GetOrders(Guid userId)
        {
            var orders = await FanOutAsync<IPurchaseOrchestrator, OrderRecord>(OrchestratorUri, o => o.GetOrders(userId));
            return orders.OrderByDescending(o => o.CreatedUtc).ToList();
        }

        public async Task<List<Book>> ListAvailableItems()
        {
            var books = await FanOutAsync<IBookstoreService, Book>(BookstoreUri, b => b.ListAvailableItemsAsync());
            return books.OrderBy(b => b.BookName).ToList();
        }

        public async Task<List<User>> ListClients()
        {
            var users = await FanOutAsync<IBankService, User>(BankUri, b => b.ListClientsAsync());
            return users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList();
        }

        /// <summary>Route by purchase id (= idempotency key): a repeated request lands on the partition that owns the purchase.</summary>
        private static IPurchaseOrchestrator Orchestrator(Guid purchaseId) =>
            ServiceProxy.Create<IPurchaseOrchestrator>(OrchestratorUri, PartitionHelper.ForPurchase(purchaseId));

        /// <summary>Calls every partition of the service in parallel and merges the results.</summary>
        private static async Task<List<TItem>> FanOutAsync<TService, TItem>(Uri serviceUri, Func<TService, Task<List<TItem>>> call)
            where TService : Microsoft.ServiceFabric.Services.Remoting.IService
        {
            List<ServicePartitionKey> partitionKeys = await PartitionHelper.GetAllPartitionKeysAsync(serviceUri);
            var perPartition = await Task.WhenAll(partitionKeys.Select(key => call(ServiceProxy.Create<TService>(serviceUri, key))));
            return perPartition.SelectMany(items => items).ToList();
        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners() =>
            this.CreateServiceRemotingInstanceListeners();
    }
}
