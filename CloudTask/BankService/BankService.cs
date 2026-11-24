using System.Fabric;
using BankService.Database;
using BankService.Saga;
using Common;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace BankService
{
    /// <summary>
    /// Saga participant for money. Reliable collections:
    ///   users          UserId → User            (balance)
    ///   charges        TxId   → Charge          (one per saga; makes every command idempotent)
    ///   balanceOutbox  queue of BalanceChanged  (written in the same transaction as the balance change)
    /// </summary>
    internal sealed class BankService : StatefulService, IBankService
    {
        private const string UsersName = "users";
        private const string ChargesName = "charges";
        private const string OutboxName = "balanceOutbox";
        private const int MaxUsersToLoad = 10;

        private readonly GetUserQueries _get;
        private readonly PostUserQueries _post;

        public BankService(StatefulServiceContext context)
            : base(context)
        {
            string connectionString = DatabaseSettings.GetConnectionString(context);
            _get = new GetUserQueries(connectionString);
            _post = new PostUserQueries(connectionString);
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners() =>
            this.CreateServiceRemotingReplicaListeners();

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await InitUser(cancellationToken);
            await PumpOutboxAsync(cancellationToken);
        }

        // ---------------- queries ----------------

        public async Task<List<User>> ListClientsAsync()
        {
            var users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, User>>(UsersName);
            var result = new List<User>();
            using var tx = StateManager.CreateTransaction();
            var enumerator = (await users.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
            while (await enumerator.MoveNextAsync(CancellationToken.None))
                result.Add(enumerator.Current.Value);
            return result;
        }

        // ---------------- saga commands ----------------

        public async Task<StepResult> ChargeAsync(Guid txId, Guid userId, double amount)
        {
            var users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, User>>(UsersName);
            var charges = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, Charge>>(ChargesName);
            var outbox = await StateManager.GetOrAddAsync<IReliableQueue<BalanceChanged>>(OutboxName);

            using var tx = StateManager.CreateTransaction();

            var existing = await charges.TryGetValueAsync(tx, txId, LockMode.Update);
            if (existing.HasValue)   // repeated command: same answer as the first time
            {
                return existing.Value.Status == ChargeStatus.Charged
                    ? StepResult.Ok("Already charged.")
                    : StepResult.Fail("Charge was rejected or already refunded.");
            }

            var user = await users.TryGetValueAsync(tx, userId, LockMode.Update);
            if (!user.HasValue)
                return await RejectAsync(tx, charges, txId, userId, "User does not exist.");
            if (user.Value.Balance < amount)
                return await RejectAsync(tx, charges, txId, userId, "Not enough money.");

            User updated = user.Value;
            updated.Balance -= amount;
            await users.SetAsync(tx, userId, updated);
            await charges.AddAsync(tx, txId, new Charge { TxId = txId, UserId = userId, Amount = amount, Status = ChargeStatus.Charged });
            await outbox.EnqueueAsync(tx, new BalanceChanged { UserId = userId, Balance = updated.Balance });
            await tx.CommitAsync();
            return StepResult.Ok("Charged.");
        }

        public async Task<StepResult> RefundAsync(Guid txId, Guid userId)
        {
            var users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, User>>(UsersName);
            var charges = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, Charge>>(ChargesName);
            var outbox = await StateManager.GetOrAddAsync<IReliableQueue<BalanceChanged>>(OutboxName);

            using var tx = StateManager.CreateTransaction();

            var c = await charges.TryGetValueAsync(tx, txId, LockMode.Update);
            if (!c.HasValue)
            {
                // Refund before Charge (Charge timed out / still on its way): tombstone, a late Charge will do nothing
                await charges.AddAsync(tx, txId, new Charge { TxId = txId, UserId = userId, Status = ChargeStatus.Tombstone });
                await tx.CommitAsync();
                return StepResult.Ok("Nothing was charged; tombstone written.");
            }
            if (c.Value.Status != ChargeStatus.Charged) return StepResult.Ok("Nothing to refund.");

            Charge charge = c.Value;
            User user = (await users.TryGetValueAsync(tx, charge.UserId, LockMode.Update)).Value;
            user.Balance += charge.Amount;
            charge.Status = ChargeStatus.Refunded;
            await users.SetAsync(tx, user.UserId, user);
            await charges.SetAsync(tx, txId, charge);
            await outbox.EnqueueAsync(tx, new BalanceChanged { UserId = user.UserId, Balance = user.Balance });
            await tx.CommitAsync();
            return StepResult.Ok("Refunded.");
        }

        /// <summary>A rejected charge is remembered as a tombstone, so a delayed duplicate of the same command fails too.</summary>
        private static async Task<StepResult> RejectAsync(
            Microsoft.ServiceFabric.Data.ITransaction tx, IReliableDictionary<Guid, Charge> charges,
            Guid txId, Guid userId, string reason)
        {
            await charges.AddAsync(tx, txId, new Charge { TxId = txId, UserId = userId, Status = ChargeStatus.Tombstone });
            await tx.CommitAsync();
            return StepResult.Fail(reason);
        }

        // ---------------- startup + outbox ----------------

        /// <summary>Loads the first <see cref="MaxUsersToLoad"/> users from Postgres (public.users) into the reliable dictionary.</summary>
        private async Task InitUser(CancellationToken cancellationToken)
        {
            var users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, User>>(UsersName);
            var ownRange = await PartitionHelper.GetOwnKeyRangeAsync(Context);
            List<User> usersFromDatabase = await _get.GetUsersAsync(MaxUsersToLoad, cancellationToken);
            using var tx = StateManager.CreateTransaction();
            // each partition stores only the users whose MurmurHash3(userId) falls in its key range
            foreach (var u in usersFromDatabase.Where(u => PartitionHelper.IsInRange(u.UserId, ownRange)))
                await users.TryAddAsync(tx, u.UserId, u);   // TryAdd: safe on failover/restart
            await tx.CommitAsync();
        }

        /// <summary>Outbox pump: writes every balance change to Postgres (public.users) at-least-once, dequeues only after success.</summary>
        private async Task PumpOutboxAsync(CancellationToken cancellationToken)
        {
            var outbox = await StateManager.GetOrAddAsync<IReliableQueue<BalanceChanged>>(OutboxName);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var tx = StateManager.CreateTransaction();
                    var next = await outbox.TryPeekAsync(tx);
                    if (next.HasValue)
                    {
                        await _post.UpdateUserBalanceAsync(next.Value.UserId, next.Value.Balance, cancellationToken);
                        await outbox.TryDequeueAsync(tx);
                        await tx.CommitAsync();
                        continue;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ServiceEventSource.Current.ServiceMessage(Context, "Balance outbox delivery failed, will retry: {0}", e.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
