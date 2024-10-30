using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Management.ServiceModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace TransactionCoordinator
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class TransactionCoordinator : StatelessService,ICommunicate
    {
        private static string BookStoreURI = "fabric:/CloudTask/BookStore";
        private static string BankURI = "fabric:/CloudTask/Bank";
        public TransactionCoordinator(StatelessServiceContext context)
            : base(context)
        { }

        public async Task<bool> TransferDataAndPurchase(Purchase p)
        {


            double PriceOfBook = await GetBookPrice(p.BookName);
            p.AmountOfPurchase = PriceOfBook * p.Quantity;
            Guid guid = Guid.NewGuid(); // guid for this transaction 

            //Prepare user and bookStore for buy
            if (await PrepareBank(p, guid) && await PrepareBookStore(p, guid))
            {
                await Commit(p, BookStoreURI);
                await Commit(p, BankURI);
                return true;
            }
            else
            {
                //unlock resoruces ->roll back 
                await RollBack(p, BookStoreURI);
                await RollBack(p, BankURI);
                return false;
            }
        }

        private async Task RollBack(Purchase p,string uri)
        {
            var fabricClient = new FabricClient();

            var partitionList = await fabricClient.QueryManager.GetPartitionListAsync(new Uri(uri));
            foreach (var partition in partitionList)
            {
                var partitionKey = new ServicePartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey);
                var proxy = ServiceProxy.Create<ITransaction>(new Uri(uri), partitionKey);
                await proxy.RollBack(p);
            }
        }

    

        private async Task Commit(Purchase p,string uri)
        {
            var fabricClient = new FabricClient();

            var partitionList = await fabricClient.QueryManager.GetPartitionListAsync(new Uri(BookStoreURI));
            foreach (var partition in partitionList)
            {
                var partitionKey = new ServicePartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey);
                var proxy = ServiceProxy.Create<ITransaction>(new Uri(BookStoreURI), partitionKey);
                await proxy.Commit(p);  
            }

        }


        private async Task<double> GetBookPrice(string bookName)
        {
            var fabricClient = new FabricClient();

            var partitionList = await fabricClient.QueryManager.GetPartitionListAsync(new Uri(BookStoreURI));
            var priceOfBook=-1.0;
            foreach (var partition in partitionList)
            {
                var partitionKey = new ServicePartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey);
                var proxy = ServiceProxy.Create<IBookStore>(new Uri(BookStoreURI), partitionKey);
                var proxyResult = await proxy.GetItemPrice(bookName);
                if (proxyResult > 0.0)
                {
                    priceOfBook = proxyResult;
                    break;
                }
            }
            return priceOfBook;
        }

        private async Task<bool> CheckUserBalance(Purchase p)
        {
            var fabricClient = new FabricClient();

            var partitionList = await fabricClient.QueryManager.GetPartitionListAsync(new Uri(BankURI));
            var MoneyExists = false;
            foreach (var partition in partitionList)
            {
                var partitionKey = new ServicePartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey);
                var proxy = ServiceProxy.Create<IBank>(new Uri(BankURI), partitionKey);
                var proxyResult = await proxy.CheckAmountOfMoney(p);
                if (proxyResult)
                {
                    MoneyExists = proxyResult;
                    break;
                }
            }
            return MoneyExists;
        }


        private async Task<bool> PrepareBank(Purchase p,Guid token)
        {
            var fabricClient = new FabricClient();

            var partitionList = await fabricClient.QueryManager.GetPartitionListAsync(new Uri(BankURI));
            var bankIsReady = false;
            foreach (var partition in partitionList)
            {
                var partitionKey = new ServicePartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey);
                var proxy = ServiceProxy.Create<ITransaction>(new Uri(BankURI), partitionKey);
                var proxyResult = await proxy.PrepareAndLock(p,token);
                if (proxyResult)
                {
                    bankIsReady = proxyResult;
                    break;
                }
            }
            return bankIsReady;
        }


        private async Task<bool> PrepareBookStore(Purchase p,Guid token)
        {
            var fabricClient = new FabricClient();
            var partitionList = await fabricClient.QueryManager.GetPartitionListAsync(new Uri(BookStoreURI));
            var bookStoreIsReady = false;
            foreach (var partition in partitionList)
            {
                var partitionKey = new ServicePartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey);
                var proxy = ServiceProxy.Create<ITransaction>(new Uri(BookStoreURI), partitionKey);
                var proxyResult = await proxy.PrepareAndLock(p, token);
                if (proxyResult)
                {
                    bookStoreIsReady = proxyResult;
                    break;
                }
            }
            return bookStoreIsReady;
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
