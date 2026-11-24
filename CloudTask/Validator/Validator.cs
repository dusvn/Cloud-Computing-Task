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

        public Task<List<OrderRecord>> GetOrders(Guid userId)
        {
            var orchestrator = ServiceProxy.Create<IPurchaseOrchestrator>(
                new Uri("fabric:/CloudTask/PurchaseOrchestrator"),
                PartitionHelper.GetPartitionKey(userId));
            return orchestrator.GetOrders(userId);
        }

        private static Task<PurchaseResponse> ForwardToOrchestratorAsync(Purchase p)
        {
            // route to the orchestrator partition owning this client (hash of UserId)
            var orchestrator = ServiceProxy.Create<IPurchaseOrchestrator>(
                new Uri("fabric:/CloudTask/PurchaseOrchestrator"),
                PartitionHelper.GetPartitionKey(p.UserId));
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
