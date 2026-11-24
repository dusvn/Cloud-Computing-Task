using System.Fabric;
using Common;
using PurchaseOrchestrator.Database;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using ITransaction = Common.Interfaces.ITransaction;

namespace PurchaseOrchestrator
{
    /// <summary>
    /// Stateful service (4 partitions) that replaces the old stateless TransactionCoordinator.
    /// For now it still runs the 2PC flow; the saga is a later step.
    /// Purchases are idempotent: every order is stored under its idempotency key.
    /// </summary>
    internal sealed class PurchaseOrchestrator : StatefulService, IPurchaseOrchestrator
    {
        private static readonly Uri BookstoreUri = new("fabric:/CloudTask/BookstoreService");
        private static readonly Uri BankUri = new("fabric:/CloudTask/BankService");
        private const string OrdersDictionaryName = "orders";

        private readonly PostOrderQueries _post;

        public PurchaseOrchestrator(StatefulServiceContext context)
            : base(context)
        {
            _post = new PostOrderQueries(DatabaseSettings.GetConnectionString(context));
        }

        /// <summary>
        /// Idempotent purchase. The order is registered under its idempotency key (reliable dictionary, replicated)
        /// BEFORE the 2PC starts, so a second request with the same key never starts a second purchase.
        /// </summary>
        public async Task<PurchaseResponse> TransferDataAndPurchase(Purchase p)
        {
            var orders = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, OrderRecord>>(OrdersDictionaryName);

            OrderRecord order;
            using (var tx = StateManager.CreateTransaction())
            {
                // Update lock: a concurrent duplicate waits here until the first request has committed its record.
                var existing = await orders.TryGetValueAsync(tx, p.IdempotencyKey, LockMode.Update);
                if (existing.HasValue)
                    return ReplayResponse(existing.Value);

                var now = DateTime.UtcNow;
                order = new OrderRecord
                {
                    IdempotencyKey = p.IdempotencyKey,
                    TransactionId = Guid.NewGuid(),
                    UserId = p.UserId,
                    BookId = p.BookId,
                    BookName = p.BookName,
                    Quantity = p.Quantity,
                    Status = OrderStatus.Pending,
                    Message = "Order is being processed.",
                    CreatedUtc = now,
                    UpdatedUtc = now
                };
                await orders.AddAsync(tx, order.IdempotencyKey, order);
                await tx.CommitAsync();
            }
            await SaveToDatabaseAsync(order);

            bool purchased;
            try
            {
                purchased = await RunTwoPhaseCommit(p, order);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ServiceEventSource.Current.Message("Order {0} failed: {1}", order.TransactionId, e.Message);
                purchased = false;
            }

            order.Status = purchased ? OrderStatus.Completed : OrderStatus.Failed;
            order.Message = purchased ? "Order successfully created." : "Purchase could not be completed.";
            order.UpdatedUtc = DateTime.UtcNow;
            using (var tx = StateManager.CreateTransaction())
            {
                await orders.SetAsync(tx, order.IdempotencyKey, order);
                await tx.CommitAsync();
            }
            await SaveToDatabaseAsync(order);

            var response = purchased ? PurchaseResponse.Ok(order.Message) : PurchaseResponse.Conflict(order.Message);
            response.TransactionId = order.TransactionId;
            response.IdempotencyKey = order.IdempotencyKey;
            return response;
        }

        public async Task<List<OrderRecord>> GetOrders(Guid userId)
        {
            var orders = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, OrderRecord>>(OrdersDictionaryName);
            var result = new List<OrderRecord>();
            using (var tx = StateManager.CreateTransaction())
            {
                var enumerable = await orders.CreateEnumerableAsync(tx);
                var e = enumerable.GetAsyncEnumerator();
                while (await e.MoveNextAsync(CancellationToken.None))
                    if (e.Current.Value.UserId == userId) result.Add(e.Current.Value);
            }
            return result.OrderByDescending(o => o.CreatedUtc).ToList();
        }

        /// <summary>What a repeated request gets back: the stored outcome of the first one.</summary>
        private static PurchaseResponse ReplayResponse(OrderRecord o)
        {
            PurchaseResponse r = o.Status switch
            {
                OrderStatus.Completed => PurchaseResponse.Ok("Order is already successfully created."),
                OrderStatus.Failed => PurchaseResponse.Conflict(o.Message),
                // not finished yet: the order exists, the user must not (and need not) buy again
                _ => PurchaseResponse.Accepted("Order is successfully created and is being processed.")
            };
            r.TransactionId = o.TransactionId;
            r.IdempotencyKey = o.IdempotencyKey;
            r.IsDuplicate = true;
            return r;
        }

        /// <summary>Postgres mirror of the reliable dictionary; the dictionary stays the source of truth.</summary>
        private async Task SaveToDatabaseAsync(OrderRecord order)
        {
            try { await _post.SaveOrderAsync(order); }
            catch (Exception e)
            {
                ServiceEventSource.Current.Message("Saving order {0} to Postgres failed: {1}", order.TransactionId, e.Message);
            }
        }

        private static async Task<bool> RunTwoPhaseCommit(Purchase p, OrderRecord order)
        {
            // Bookstore partition is keyed by BookId, Bank partition by UserId.
            var bookstore = ServiceProxy.Create<IBookstoreService>(BookstoreUri, PartitionHelper.GetPartitionKey(p.BookId));
            double price = await bookstore.GetItemPrice(p.BookId);
            if (price <= 0.0) return false;

            p.AmountOfPurchase = price * p.Quantity;
            order.Amount = p.AmountOfPurchase;
            Guid txId = order.TransactionId;
            p.PurchaseId = txId;

            var bookTx = ServiceProxy.Create<ITransaction>(BookstoreUri, PartitionHelper.GetPartitionKey(p.BookId));
            var bankTx = ServiceProxy.Create<ITransaction>(BankUri, PartitionHelper.GetPartitionKey(p.UserId));

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
