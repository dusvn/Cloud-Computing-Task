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
        public BankService(StatefulServiceContext context)
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



        private async Task InitUser()
        {
            var users = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, User>>("users");
            int ownIndex = await PartitionHelper.GetOwnPartitionIndexAsync(this.Context);
            var seed = new[]
            {
                new User("Dusan", "Paripovic", 165.18, "example@example.com"),
                new User("Marko", "Markovic", 200.00, "marko@example.com"),
                new User("Ana", "Anic", 120.50, "ana@example.com"),
                new User("Petar", "Petrovic", 300.00, "petar@example.com"),
            };
            using (var tx = StateManager.CreateTransaction())
            {
                // each partition stores only the users whose hash(userId) maps to it
                foreach (var u in seed.Where(u => PartitionHelper.GetPartitionIndex(u.Email) == ownIndex))
                    await users.TryAddAsync(tx, u.Email, u);   // TryAdd: safe on failover/restart
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
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<string,Guid>>("lockedUsers");
            var users = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, User>>("users");
            await InitUser();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        public async Task Commit(Purchase purchase)
        {
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Guid>>("lockedUsers");
            var users = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, User>>("users");

            using (var transaction = StateManager.CreateTransaction()) 
            {
                ConditionalValue<User> user = await users.TryGetValueAsync(transaction, purchase.Email);
                ConditionalValue<Guid> locked = await lockedResources.TryGetValueAsync(transaction, purchase.Email);    
                
                var updatedUser = user.Value;
                updatedUser.Balance -= purchase.AmountOfPurchase;

                await users.AddOrUpdateAsync(transaction, updatedUser.Email, updatedUser, (key, oldValue) => updatedUser);
                await lockedResources.TryRemoveAsync(transaction, purchase.Email);  //unlock

                await transaction.CommitAsync();

            }
        }

        public async Task<bool> PrepareAndLock(Purchase purchase, Guid token)
        {
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Guid>>("lockedUsers");
            var users = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, User>>("users");


            using (var tx = this.StateManager.CreateTransaction())
            {
                ConditionalValue<User> user = await users.TryGetValueAsync(tx, purchase.Email);
                ConditionalValue<Guid> lockedUser = await lockedResources.TryGetValueAsync(tx, purchase.Email);
                if (user.HasValue && user.Value.Balance >= purchase.AmountOfPurchase) 
                {
                    //check is user currently used for some operation
                    if (lockedUser.HasValue)
                    { 
                        await tx.CommitAsync();
                        return false;
                    }

                    //lock user 
                    await lockedResources.AddAsync(tx,purchase.Email,token);
                    await tx.CommitAsync();
                    return true;
                }

            }


            return false;
        }


        public async Task RollBack(Purchase p)
        {
            var lockedResources = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Guid>>("lockedUsers");
            using (var transaction = StateManager.CreateTransaction())
            {
                await lockedResources.TryRemoveAsync(transaction, p.Email);  //unlock
                await transaction.CommitAsync();

            }
        }

    }
}
