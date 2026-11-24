using System.Collections.Concurrent;
using System.Fabric;
using Common;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using PurchaseOrchestrator.Database;
using PurchaseOrchestrator.Saga;

namespace PurchaseOrchestrator
{
    /// <summary>
    /// Saga orchestrator (stateful, 4 partitions keyed by purchase id = idempotency key).
    ///
    /// Durability rule for every step: write the new state FIRST (Postgres saga_log, then the replicated
    /// reliable dictionary "sagas"), and only then execute the next command. So whatever happens, the
    /// stored state says which command has to run next, and every command is idempotent, so running it
    /// again after a crash is safe. RunAsync resumes all unfinished sagas on the new primary.
    /// </summary>
    internal sealed class PurchaseOrchestrator : StatefulService, IPurchaseOrchestrator
    {
        private const string SagasName = "sagas";
        private static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

        private readonly PostOrderQueries _orders;
        private readonly PostSagaLogQueries _logWrite;
        private readonly GetSagaLogQueries _logRead;

        /// <summary>Sagas being driven right now on this replica, so the request and the recovery loop never drive the same saga twice.</summary>
        private readonly ConcurrentDictionary<Guid, byte> _running = new();

        public PurchaseOrchestrator(StatefulServiceContext context)
            : base(context)
        {
            string connectionString = DatabaseSettings.GetConnectionString(context);
            _orders = new PostOrderQueries(connectionString);
            _logWrite = new PostSagaLogQueries(connectionString);
            _logRead = new GetSagaLogQueries(connectionString);
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners() =>
            this.CreateServiceRemotingReplicaListeners();

        // ---------------- API ----------------

        /// <summary>
        /// Starts the saga for a new idempotency key and drives it as far as possible.
        /// A repeated key never starts a second saga: it gets the state of the existing one.
        /// </summary>
        public async Task<PurchaseResponse> TransferDataAndPurchase(Purchase p)
        {
            var sagas = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, OrderRecord>>(SagasName);

            using (var tx = StateManager.CreateTransaction())
            {
                // Update lock: a concurrent duplicate waits here until the first request has stored its saga.
                var existing = await sagas.TryGetValueAsync(tx, p.IdempotencyKey, LockMode.Update);
                if (existing.HasValue)
                    return ToResponse(existing.Value, isDuplicate: true);

                var now = DateTime.UtcNow;
                var saga = new OrderRecord
                {
                    IdempotencyKey = p.IdempotencyKey,
                    TransactionId = Guid.NewGuid(),
                    UserId = p.UserId,
                    BookId = p.BookId,
                    BookName = p.BookName,
                    Email = p.Email,
                    Quantity = p.Quantity,
                    Status = SagaState.Started,
                    Message = "Saga started.",
                    CreatedUtc = now,
                    UpdatedUtc = now
                };
                await _logWrite.AppendAsync(saga);          // 1. saga log (Postgres)
                await sagas.AddAsync(tx, saga.IdempotencyKey, saga);
                await tx.CommitAsync();                     // 2. reliable dictionary (replicated)
                await MirrorOrderAsync(saga);
            }

            await DriveAsync(p.IdempotencyKey);

            OrderRecord? current = await ReadSagaAsync(p.IdempotencyKey);
            return ToResponse(current!, isDuplicate: false);
        }

        public async Task<List<OrderRecord>> GetOrders(Guid userId)
        {
            var sagas = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, OrderRecord>>(SagasName);
            var result = new List<OrderRecord>();
            using (var tx = StateManager.CreateTransaction())
            {
                var enumerator = (await sagas.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(CancellationToken.None))
                    if (enumerator.Current.Value.UserId == userId) result.Add(enumerator.Current.Value);
            }
            return result.OrderByDescending(o => o.CreatedUtc).ToList();
        }

        // ---------------- saga engine ----------------

        /// <summary>
        /// Runs the saga step by step until it finishes or a step fails technically (then the recovery loop retries later).
        /// Each step: look up the command for the current state → execute it → store the next state.
        /// </summary>
        private async Task DriveAsync(Guid key)
        {
            if (!_running.TryAdd(key, 0)) return;   // already being driven on this replica
            try
            {
                while (true)
                {
                    OrderRecord? saga = await ReadSagaAsync(key);
                    SagaStep? step = saga is null ? null : PurchaseSaga.StepFor(saga.Status);
                    if (saga is null || step is null) return;   // unknown or finished

                    StepResult result;
                    try
                    {
                        result = await step.Command.ExecuteAsync(saga);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        await RecordFailedAttemptAsync(saga, step, e);
                        return;
                    }

                    SagaState next = result.Succeeded ? step.OnSuccess : step.OnFailure;
                    string message = next == SagaState.Compensated
                        ? $"Purchase cancelled: {saga.Message}"   // keep the reason why compensation started
                        : result.Message;
                    if (!await TryMoveAsync(saga, next, message)) return;
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // e.g. Postgres down while writing the log: nothing is lost, the recovery loop tries again
                ServiceEventSource.Current.Message("Saga {0} paused: {1}", key, e.Message);
            }
            finally
            {
                _running.TryRemove(key, out _);
            }
        }

        /// <summary>Technical failure of a step: count it; before the pivot give up after N attempts and compensate.</summary>
        private async Task RecordFailedAttemptAsync(OrderRecord saga, SagaStep step, Exception error)
        {
            int attempts = saga.Attempts + 1;
            ServiceEventSource.Current.Message("Saga {0}: {1} attempt {2} failed: {3}", saga.IdempotencyKey, step.Command.Name, attempts, error.Message);

            if (step.BeforePivot && attempts >= PurchaseSaga.MaxAttemptsBeforePivot)
            {
                await TryMoveAsync(saga, SagaState.Compensating, $"{step.Command.Name} failed {attempts} times: {error.Message}");
                return;
            }
            await TryMoveAsync(saga, saga.Status, $"{step.Command.Name} attempt {attempts} failed: {error.Message}", attempts);
        }

        /// <summary>
        /// Stores the saga in its next state: saga_log row first, then the reliable dictionary.
        /// Returns false when another driver already moved the saga on (nothing is overwritten then).
        /// </summary>
        private async Task<bool> TryMoveAsync(OrderRecord saga, SagaState next, string message, int attempts = 0)
        {
            SagaState expected = saga.Status;
            saga.Status = next;
            saga.Message = message;
            saga.Attempts = attempts;
            saga.UpdatedUtc = DateTime.UtcNow;

            await _logWrite.AppendAsync(saga);   // write-ahead: the log never lags behind the dictionary

            var sagas = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, OrderRecord>>(SagasName);
            using (var tx = StateManager.CreateTransaction())
            {
                var stored = await sagas.TryGetValueAsync(tx, saga.IdempotencyKey, LockMode.Update);
                if (!stored.HasValue || stored.Value.Status != expected) return false;
                await sagas.SetAsync(tx, saga.IdempotencyKey, saga);
                await tx.CommitAsync();
            }
            await MirrorOrderAsync(saga);
            return true;
        }

        private async Task<OrderRecord?> ReadSagaAsync(Guid key)
        {
            var sagas = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, OrderRecord>>(SagasName);
            using var tx = StateManager.CreateTransaction();
            var value = await sagas.TryGetValueAsync(tx, key);
            return value.HasValue ? value.Value : null;
        }

        // ---------------- recovery ----------------

        /// <summary>
        /// Runs on every new primary (first start, failover, restart):
        ///  1. sagas of this partition that are in saga_log but missing in the dictionary are put back;
        ///  2. forever: every unfinished saga whose backoff has passed is driven again.
        /// </summary>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await RestoreFromLogAsync(cancellationToken);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    foreach (Guid key in await GetDueSagasAsync())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await DriveAsync(key);
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ServiceEventSource.Current.Message("Saga recovery loop failed, will retry: {0}", e.Message);
                }
                await Task.Delay(RecoveryInterval, cancellationToken);
            }
        }

        private async Task RestoreFromLogAsync(CancellationToken cancellationToken)
        {
            var ownRange = await PartitionHelper.GetOwnKeyRangeAsync(Context);
            var sagas = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, OrderRecord>>(SagasName);
            while (true)
            {
                try
                {
                    List<OrderRecord> unfinished = await _logRead.GetUnfinishedSagasAsync(cancellationToken);
                    using var tx = StateManager.CreateTransaction();
                    // the dictionary wins when it has the saga; only missing ones of THIS partition are restored
                    foreach (var saga in unfinished.Where(s => PartitionHelper.IsInRange(s.IdempotencyKey, ownRange)))
                        await sagas.TryAddAsync(tx, saga.IdempotencyKey, saga);
                    await tx.CommitAsync();
                    return;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ServiceEventSource.Current.Message("Restoring sagas from saga_log failed, will retry: {0}", e.Message);
                    await Task.Delay(RecoveryInterval, cancellationToken);
                }
            }
        }

        /// <summary>Unfinished sagas whose retry backoff (2^attempts seconds, max 30s) has passed.</summary>
        private async Task<List<Guid>> GetDueSagasAsync()
        {
            var sagas = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, OrderRecord>>(SagasName);
            var due = new List<Guid>();
            var now = DateTime.UtcNow;
            using var tx = StateManager.CreateTransaction();
            var enumerator = (await sagas.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
            while (await enumerator.MoveNextAsync(CancellationToken.None))
            {
                OrderRecord s = enumerator.Current.Value;
                if (s.IsFinished || _running.ContainsKey(s.IdempotencyKey)) continue;
                var backoff = s.Attempts == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, s.Attempts)));
                if (s.UpdatedUtc + backoff <= now) due.Add(s.IdempotencyKey);
            }
            return due;
        }

        // ---------------- helpers ----------------

        private static PurchaseResponse ToResponse(OrderRecord saga, bool isDuplicate)
        {
            PurchaseResponse r = saga.Status switch
            {
                SagaState.Completed => PurchaseResponse.Ok(isDuplicate ? "Order is already successfully created." : "Order successfully created."),
                SagaState.Compensated or SagaState.Failed => PurchaseResponse.Conflict(saga.Message),
                // saga still running (or waiting for a retry): the order exists, the user must not buy again
                _ => PurchaseResponse.Accepted("Order is successfully created and is being processed.")
            };
            r.TransactionId = saga.TransactionId;
            r.IdempotencyKey = saga.IdempotencyKey;
            r.IsDuplicate = isDuplicate;
            return r;
        }

        /// <summary>public.orders is only a read view of the current state; saga_log + dictionary are the source of truth.</summary>
        private async Task MirrorOrderAsync(OrderRecord saga)
        {
            try { await _orders.SaveOrderAsync(saga); }
            catch (Exception e)
            {
                ServiceEventSource.Current.Message("Saving order {0} to Postgres failed: {1}", saga.TransactionId, e.Message);
            }
        }
    }
}
