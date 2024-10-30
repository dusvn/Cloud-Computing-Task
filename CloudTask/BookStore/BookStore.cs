using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using ITransaction = Common.Interfaces.ITransaction;

namespace BookStore
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class BookStore : StatefulService,IService,ITransaction,IBookStore
    {
        public BookStore(StatefulServiceContext context)
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
            try
            {
                using(var tx = this.StateManager.CreateTransaction())
                {
                    await bookStore.AddAsync(tx, "Knjiga1", new Book("Knjiga1", 10,10.26));
                    await bookStore.AddAsync(tx, "Knjiga2", new Book("Knjiga2", 10, 26.10));
                    await bookStore.AddAsync(tx, "Knjiga3", new Book("Knjiga3", 10, 15.24));
                    await tx.CommitAsync();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");
            var bookStore = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("bookStore");
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Guid>>("lockedBooks");
            await InitBooks();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "Counter");

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                        result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
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

        public Task<bool> UnlockResource(Purchase purchase, Guid token)
        {
            throw new NotImplementedException();
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
