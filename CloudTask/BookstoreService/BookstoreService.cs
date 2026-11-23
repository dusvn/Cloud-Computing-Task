using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using ITransaction = Common.Interfaces.ITransaction;

namespace BookstoreService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class BookstoreService : StatefulService,IService,ITransaction,IBookstoreService
    {
        public BookstoreService(StatefulServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }


        public async Task InitBooks()
        {
            var bookStore = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("bookStore");
            int ownIndex = await PartitionHelper.GetOwnPartitionIndexAsync(this.Context);
            var seed = new[]
            {
                new Book("Knjiga1", 10, 10.26), new Book("Knjiga2", 10, 26.10), new Book("Knjiga3", 10, 15.24),
                new Book("Knjiga4", 10, 12.50), new Book("Knjiga5", 10, 8.99),  new Book("Knjiga6", 10, 19.90),
                new Book("Knjiga7", 10, 22.00), new Book("Knjiga8", 10, 5.75),
            };
            using (var tx = this.StateManager.CreateTransaction())
            {
                // each partition stores only the books whose hash(bookId) maps to it
                foreach (var b in seed.Where(b => PartitionHelper.GetPartitionIndex(b.BookName) == ownIndex))
                    await bookStore.TryAddAsync(tx, b.BookName, b);   // TryAdd: safe on failover/restart
                await tx.CommitAsync();
            }
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await InitBooks();

            // Outbox pump: sales are enqueued atomically with the stock update (see Commit);
            // here they are delivered to ReportService at-least-once. The consumer is idempotent.
            var outbox = await this.StateManager.GetOrAddAsync<IReliableQueue<SaleEvent>>("salesOutbox");
            var reportService = ServiceProxy.Create<IReportService>(new Uri("fabric:/CloudTask/ReportService"));
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using (var tx = this.StateManager.CreateTransaction())
                    {
                        var next = await outbox.TryPeekAsync(tx);
                        if (next.HasValue)
                        {
                            await reportService.ConsumeSaleAsync(next.Value);
                            await outbox.TryDequeueAsync(tx);
                            await tx.CommitAsync();
                            continue;   // more messages may be waiting
                        }
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Outbox delivery failed, will retry: {0}", e.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        public async Task Commit(Purchase purchase)
        {
            var bookStore = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("bookStore");
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Guid>>("lockedBooks");

            using (var transaction = StateManager.CreateTransaction())
            {
                ConditionalValue<Book> book = await bookStore.TryGetValueAsync(transaction, purchase.BookName);
                ConditionalValue<Guid> locked = await lockedResources.TryGetValueAsync(transaction, purchase.Email);

                var updatedBook = book.Value;
                updatedBook.Quantity -= purchase.Quantity;

                await bookStore.AddOrUpdateAsync(transaction, updatedBook.BookName, updatedBook, (key, oldValue) => updatedBook); //update 

                // outbox: same transaction as the stock update, so a crash cannot lose the sale
                var outbox = await this.StateManager.GetOrAddAsync<IReliableQueue<SaleEvent>>("salesOutbox");
                await outbox.EnqueueAsync(transaction, new SaleEvent
                {
                    PurchaseId = purchase.PurchaseId,
                    BookName = purchase.BookName,
                    Quantity = purchase.Quantity,
                    Amount = purchase.AmountOfPurchase,
                    Email = purchase.Email,
                    SoldAtUtc = DateTime.UtcNow
                });
                await lockedResources.TryRemoveAsync(transaction, purchase.BookName);  //unlock

                await transaction.CommitAsync();

            }
        }

        public async Task<bool> PrepareAndLock(Purchase purchase, Guid token)
        {
            var bookStore = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("bookStore");
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Guid>>("lockedBooks");
            using (var tx = this.StateManager.CreateTransaction()) 
            {
                ConditionalValue<Book> book = await bookStore.TryGetValueAsync(tx, purchase.BookName);
                ConditionalValue<Guid> lockedResource = await lockedResources.TryGetValueAsync(tx, purchase.BookName);
                if (book.HasValue && purchase.Quantity <=book.Value.Quantity)
                {
                    //if someone acting with book (resource is locked)
                    if (lockedResource.HasValue) 
                    {
                        await tx.CommitAsync();
                        return false;
                    }

                    await lockedResources.AddAsync(tx, purchase.BookName, token);
                    await tx.CommitAsync();
                    return true;

                }
            }

            return false;   
            
        }


        public async Task RollBack(Purchase purchase)
        {
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Guid>>("lockedBooks");
            using (var transaction = StateManager.CreateTransaction())
            {
                await lockedResources.TryRemoveAsync(transaction,purchase.BookName);  //unlock
                await transaction.CommitAsync();

            }
        }

        public async Task<double> GetItemPrice(string bookName)
        {
            var bookStore = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("bookStore");
            using(var tx = this.StateManager.CreateTransaction())
            {
                ConditionalValue<Book> book = await bookStore.TryGetValueAsync(tx, bookName);
                if (book.HasValue)
                {
                    return book.Value.Price;
                }
                else return -1.0;
            }
        }
    }
}
