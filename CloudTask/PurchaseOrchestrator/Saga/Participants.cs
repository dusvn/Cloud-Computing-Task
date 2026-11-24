using Common;
using Common.Interfaces;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace PurchaseOrchestrator.Saga
{
    /// <summary>Proxies to the saga participants, routed to the partition that owns the book / the user.</summary>
    internal static class Participants
    {
        private static readonly Uri BookstoreUri = new("fabric:/CloudTask/BookstoreService");
        private static readonly Uri BankUri = new("fabric:/CloudTask/BankService");

        public static IBookstoreService Bookstore(Guid bookId) =>
            ServiceProxy.Create<IBookstoreService>(BookstoreUri, PartitionHelper.GetPartitionKey(bookId));

        public static IBankService Bank(Guid userId) =>
            ServiceProxy.Create<IBankService>(BankUri, PartitionHelper.GetPartitionKey(userId));
    }
}
