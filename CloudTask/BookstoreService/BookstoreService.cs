using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using BookstoreService.Database;
using Common.Interfaces;
using Common.Models;
using Microsoft.EntityFrameworkCore;
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
        private readonly GetBookQueries _get;
        private readonly PostBookQueries _post;

        public BookstoreService(StatefulServiceContext context)
            : base(context)
        {
            string connectionString = DatabaseSettings.GetConnectionString(context);
            _get = new GetBookQueries(connectionString);
            _post = new PostBookQueries(connectionString);
        }

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


        private const int MaxBooksToLoad = 10;

        /// <summary>Loads the first <see cref="MaxBooksToLoad"/> books from Postgres (public.books) into the reliable dictionary.</summary>
        public async Task InitBooks(CancellationToken cancellationToken)
        {
            var bookStore = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Book>>("bookStore");
            var ownRange = await PartitionHelper.GetOwnKeyRangeAsync(this.Context);
            List<Book> booksFromDatabase = await _get.GetBooksAsync(MaxBooksToLoad, cancellationToken);
            using (var tx = this.StateManager.CreateTransaction())
            {
                // each partition stores only the books whose MurmurHash3(bookId) falls in its key range
                foreach (var b in booksFromDatabase.Where(b => PartitionHelper.IsInRange(b.BookId, ownRange)))
                    await bookStore.TryAddAsync(tx, b.BookId, b);   // TryAdd: safe on failover/restart
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
            await InitBooks(cancellationToken);

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
            var bookStore = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Book>>("bookStore");
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Guid>>("lockedBooks");

            using (var transaction = StateManager.CreateTransaction())
            {
                ConditionalValue<Book> book = await bookStore.TryGetValueAsync(transaction, purchase.BookId);
                ConditionalValue<Guid> locked = await lockedResources.TryGetValueAsync(transaction, purchase.BookId);

                var updatedBook = book.Value;
                updatedBook.Quantity -= purchase.Quantity;

                await bookStore.AddOrUpdateAsync(transaction, updatedBook.BookId, updatedBook, (key, oldValue) => updatedBook); //update 

                // outbox: same transaction as the stock update, so a crash cannot lose the sale
                var outbox = await this.StateManager.GetOrAddAsync<IReliableQueue<SaleEvent>>("salesOutbox");
                await outbox.EnqueueAsync(transaction, new SaleEvent
                {
                    PurchaseId = purchase.PurchaseId,
                    BookId = purchase.BookId,
                    UserId = purchase.UserId,
                    BookName = purchase.BookName,
                    Quantity = purchase.Quantity,
                    Amount = purchase.AmountOfPurchase,
                    Email = purchase.Email,
                    SoldAtUtc = DateTime.UtcNow
                });
                await lockedResources.TryRemoveAsync(transaction, purchase.BookId);  //unlock

                await transaction.CommitAsync();

                await SaveQuantityToDatabaseAsync(updatedBook.BookId, updatedBook.Quantity);
            }
        }

        /// <summary>Writes the new stock back to public.books (absolute value, so a retry is harmless).</summary>
        private async Task SaveQuantityToDatabaseAsync(Guid bookId, int quantity)
        {
            try
            {
                await _post.UpdateBookQuantityAsync(bookId, quantity);
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceMessage(Context, "Saving stock of book {0} to Postgres failed: {1}", bookId, e.Message);
            }
        }

        public async Task<bool> PrepareAndLock(Purchase purchase, Guid token)
        {
            var bookStore = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Book>>("bookStore");
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Guid>>("lockedBooks");
            using (var tx = this.StateManager.CreateTransaction()) 
            {
                ConditionalValue<Book> book = await bookStore.TryGetValueAsync(tx, purchase.BookId);
                ConditionalValue<Guid> lockedResource = await lockedResources.TryGetValueAsync(tx, purchase.BookId);
                if (book.HasValue && purchase.Quantity <=book.Value.Quantity)
                {
                    //if someone acting with book (resource is locked)
                    if (lockedResource.HasValue) 
                    {
                        await tx.CommitAsync();
                        return false;
                    }

                    await lockedResources.AddAsync(tx, purchase.BookId, token);
                    await tx.CommitAsync();
                    return true;

                }
            }

            return false;   
            
        }


        public async Task RollBack(Purchase purchase)
        {
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Guid>>("lockedBooks");
            using (var transaction = StateManager.CreateTransaction())
            {
                await lockedResources.TryRemoveAsync(transaction, purchase.BookId);  //unlock
                await transaction.CommitAsync();

            }
        }

        public async Task<double> GetItemPrice(Guid bookId)
        {
            var bookStore = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Book>>("bookStore");
            using(var tx = this.StateManager.CreateTransaction())
            {
                ConditionalValue<Book> book = await bookStore.TryGetValueAsync(tx, bookId);
                if (book.HasValue)
                {
                    return book.Value.Price;
                }
                else return -1.0;
            }
        }
    }
}
