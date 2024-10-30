using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

        public async Task<bool> TransferData(Purchase p)
        {
            bool validate = !string.IsNullOrEmpty(p.BookName) &&
                            !string.IsNullOrEmpty(p.FirstName) &&
                            !string.IsNullOrEmpty(p.LastName) &&
                            p.Quantity > 0 &&
                            CheckEmailPattern(p.Email);
            bool purchase = false;
            if (validate)
            {
                purchase = await ServiceProxy.Create<ICommunicate>(new Uri("fabric:/CloudTask/TransactionCoordinator")).TransferDataAndPurchase(p); //transfer data to coordinator
            }
            return validate && purchase; 
        }

        private bool CheckEmailPattern(string email)
        {
            return Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
        }



        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
