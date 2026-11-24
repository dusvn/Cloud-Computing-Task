using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace Validator
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class Validator : StatelessService, IValidate
    {
        public Validator(StatelessServiceContext context)
            : base(context)
        { }

        private readonly PurchaseValidator _purchaseValidator = new();

        public async Task<PurchaseResponse> TransferData(Purchase p)
        {
            PurchaseResponse validation = _purchaseValidator.Validate(p);
            if (!validation.IsAccepted) return validation;   // 400 with reason

            return await ForwardToOrchestratorAsync(p);
        }

        private static readonly Uri OrchestratorUri = new("fabric:/CloudTask/PurchaseOrchestrator");

        /// <summary>Orders of one user are spread over all orchestrator partitions (keyed by purchase id): ask each one and merge.</summary>
        public async Task<List<OrderRecord>> GetOrders(Guid userId)
        {
            var partitionKeys = await PartitionHelper.GetAllPartitionKeysAsync(OrchestratorUri);
            var perPartition = await Task.WhenAll(partitionKeys.Select(key =>
                ServiceProxy.Create<IPurchaseOrchestrator>(OrchestratorUri, key).GetOrders(userId)));
            return perPartition.SelectMany(orders => orders).OrderByDescending(o => o.CreatedUtc).ToList();
        }

        private static Task<PurchaseResponse> ForwardToOrchestratorAsync(Purchase p)
        {
            // route by purchase id (= idempotency key): a duplicate request lands on the partition that owns the order
            var orchestrator = ServiceProxy.Create<IPurchaseOrchestrator>(
                OrchestratorUri,
                PartitionHelper.ForPurchase(p.IdempotencyKey));
            return orchestrator.TransferDataAndPurchase(p);
        }



        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }
    }
}
