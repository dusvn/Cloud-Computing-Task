using System.Fabric;
using Common;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace PurchaseOrchestrator
{
    /// <summary>
    /// Stateful service (4 partitions) that replaces the old stateless TransactionCoordinator.
    /// For now it still runs the 2PC flow; the saga is a later step.
    /// </summary>
    internal sealed class PurchaseOrchestrator : StatefulService, IPurchaseOrchestrator
    {
        private static readonly Uri BookstoreUri = new("fabric:/CloudTask/BookstoreService");
        private static readonly Uri BankUri = new("fabric:/CloudTask/BankService");

        public PurchaseOrchestrator(StatefulServiceContext context)
            : base(context)
        { }

        public async Task<bool> TransferDataAndPurchase(Purchase p)
        {
            // Bookstore partition is keyed by book, Bank partition by client.
            var bookstore = ServiceProxy.Create<IBookstoreService>(BookstoreUri, PartitionHelper.GetPartitionKey(p.BookName));
            double price = await bookstore.GetItemPrice(p.BookName);
            if (price <= 0.0) return false;

            p.AmountOfPurchase = price * p.Quantity;
            Guid txId = Guid.NewGuid();
            p.PurchaseId = txId;

            var bookTx = ServiceProxy.Create<ITransaction>(BookstoreUri, PartitionHelper.GetPartitionKey(p.BookName));
            var bankTx = ServiceProxy.Create<ITransaction>(BankUri, PartitionHelper.GetPartitionKey(p.Email));

            if (await bankTx.PrepareAndLock(p, txId))
            {
                if (await bookTx.PrepareAndLock(p, txId))
                {
                    await bookTx.Commit(p);
                    await bankTx.Commit(p);
                    return true;
                }
                await bankTx.RollBack(p);
                return false;
            }

            return false;
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }
    }
}
