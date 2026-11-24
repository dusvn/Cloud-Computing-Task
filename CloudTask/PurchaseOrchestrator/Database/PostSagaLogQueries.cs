using Common.Models;

namespace PurchaseOrchestrator.Database
{
    /// <summary>Every database WRITE on public.saga_log (LINQ / EF only). Append-only.</summary>
    internal sealed class PostSagaLogQueries
    {
        private readonly string _connectionString;

        public PostSagaLogQueries(string connectionString) => _connectionString = connectionString;

        /// <summary>Appends a snapshot of the saga. Throws on failure: the saga must not continue without its log row.</summary>
        public async Task AppendAsync(OrderRecord saga, CancellationToken ct = default)
        {
            await using var db = new OrdersDbContext(_connectionString);
            db.SagaLog.Add(SagaLogEntry.From(saga));
            await db.SaveChangesAsync(ct);
        }
    }
}
