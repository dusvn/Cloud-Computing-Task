using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PurchaseOrchestrator.Database
{
    /// <summary>Every database READ on public.saga_log (LINQ only).</summary>
    internal sealed class GetSagaLogQueries
    {
        private readonly string _connectionString;

        public GetSagaLogQueries(string connectionString) => _connectionString = connectionString;

        /// <summary>Newest snapshot of every saga that has not finished yet.</summary>
        public async Task<List<OrderRecord>> GetUnfinishedSagasAsync(CancellationToken ct = default)
        {
            await using var db = new OrdersDbContext(_connectionString);
            List<SagaLogEntry> latest = await db.SagaLog.AsNoTracking()
                .Where(l => l.Id == db.SagaLog
                    .Where(x => x.IdempotencyKey == l.IdempotencyKey)
                    .Max(x => x.Id))
                .Where(l => l.State != SagaState.Completed
                         && l.State != SagaState.Compensated
                         && l.State != SagaState.Failed)
                .ToListAsync(ct);
            return latest.Select(l => l.ToSaga()).ToList();
        }
    }
}
