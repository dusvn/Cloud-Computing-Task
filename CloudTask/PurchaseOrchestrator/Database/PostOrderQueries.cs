using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PurchaseOrchestrator.Database
{
    /// <summary>Every database WRITE on public.orders (LINQ only).</summary>
    internal sealed class PostOrderQueries
    {
        private readonly string _connectionString;

        public PostOrderQueries(string connectionString) => _connectionString = connectionString;

        /// <summary>Insert the order, or update its outcome when the idempotency key is already stored.</summary>
        public async Task SaveOrderAsync(OrderRecord order, CancellationToken ct = default)
        {
            await using var db = new OrdersDbContext(_connectionString);
            int updated = await db.Orders
                .Where(o => o.IdempotencyKey == order.IdempotencyKey)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, order.Status)
                    .SetProperty(o => o.Message, order.Message)
                    .SetProperty(o => o.Amount, order.Amount)
                    .SetProperty(o => o.UpdatedUtc, order.UpdatedUtc), ct);

            if (updated == 0)
            {
                db.Orders.Add(order);
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
