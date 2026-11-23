using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Common.Interfaces;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using ITransaction = Common.Interfaces.ITransaction;

namespace BankService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class BankService : StatefulService,ITransaction,IBankService
    {
        // Database connection owned by this service instance; available for any later query.
        // DbContext is not thread-safe: use it from one flow at a time (or create short-lived contexts for concurrent calls).
        private readonly BankDbContext _db;

        public BankService(StatefulServiceContext context)
            : base(context)
        {
            _db = new BankDbContext(DatabaseSettings.GetConnectionString(context));
        }

        protected override async Task OnCloseAsync(CancellationToken cancellationToken)
        {
            await _db.DisposeAsync();
            await base.OnCloseAsync(cancellationToken);
        }

        protected override void OnAbort() => _db.Dispose();

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



        private const int MaxUsersToLoad = 10;

        /// <summary>Loads the first <see cref="MaxUsersToLoad"/> users from Postgres (public.users) into the reliable dictionary.</summary>
        private async Task InitUser(CancellationToken cancellationToken)
        {
            var users = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, User>>("users");
            int ownIndex = await PartitionHelper.GetOwnPartitionIndexAsync(this.Context);
            List<User> usersFromDatabase = await _db.Users
                .AsNoTracking()
                .Take(MaxUsersToLoad)
                .ToListAsync(cancellationToken);
            using (var tx = StateManager.CreateTransaction())
            {
                // each partition stores only the users whose hash(userId) maps to it
                foreach (var u in usersFromDatabase.Where(u => PartitionHelper.GetPartitionIndex(u.UserId) == ownIndex))
                    await users.TryAddAsync(tx, u.UserId, u);   // TryAdd: safe on failover/restart
                await tx.CommitAsync();
            }
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// Loads the users from Postgres into the reliable dictionary once this replica becomes primary.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override Task RunAsync(CancellationToken cancellationToken) => InitUser(cancellationToken);

        public async Task Commit(Purchase purchase)
        {
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Guid>>("lockedUsers");
            var users = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, User>>("users");

            using (var transaction = StateManager.CreateTransaction()) 
            {
                ConditionalValue<User> user = await users.TryGetValueAsync(transaction, purchase.UserId);
                ConditionalValue<Guid> locked = await lockedResources.TryGetValueAsync(transaction, purchase.UserId);    
                
                var updatedUser = user.Value;
                updatedUser.Balance -= purchase.AmountOfPurchase;

                await users.AddOrUpdateAsync(transaction, updatedUser.UserId, updatedUser, (key, oldValue) => updatedUser);
                await lockedResources.TryRemoveAsync(transaction, purchase.UserId);  //unlock

                await transaction.CommitAsync();

            }
        }

        public async Task<bool> PrepareAndLock(Purchase purchase, Guid token)
        {
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Guid>>("lockedUsers");
            var users = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, User>>("users");


            using (var tx = this.StateManager.CreateTransaction())
            {
                ConditionalValue<User> user = await users.TryGetValueAsync(tx, purchase.UserId);
                ConditionalValue<Guid> lockedUser = await lockedResources.TryGetValueAsync(tx, purchase.UserId);
                if (user.HasValue && user.Value.Balance >= purchase.AmountOfPurchase) 
                {
                    //check is user currently used for some operation
                    if (lockedUser.HasValue)
                    { 
                        await tx.CommitAsync();
                        return false;
                    }

                    //lock user 
                    await lockedResources.AddAsync(tx,purchase.UserId,token);
                    await tx.CommitAsync();
                    return true;
                }

            }


            return false;
        }


        public async Task RollBack(Purchase p)
        {
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Guid>>("lockedUsers");
            using (var transaction = StateManager.CreateTransaction())
            {
                await lockedResources.TryRemoveAsync(transaction, p.UserId);  //unlock
                await transaction.CommitAsync();

            }
        }

    }
}
