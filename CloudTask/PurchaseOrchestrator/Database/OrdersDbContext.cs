using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PurchaseOrchestrator.Database
{
    /// <summary>The orchestrator's database connection (table public.orders). Used only by the order query classes.</summary>
    internal sealed class OrdersDbContext : DbContext
    {
        private readonly string _connectionString;

        public OrdersDbContext(string connectionString) => _connectionString = connectionString;

        public DbSet<OrderRecord> Orders => Set<OrderRecord>();

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseNpgsql(_connectionString);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderRecord>(order =>
            {
                order.ToTable("orders", "public");
                order.HasKey(o => o.IdempotencyKey);
                order.Property(o => o.IdempotencyKey).HasColumnName("idempotency_key");
                order.Property(o => o.TransactionId).HasColumnName("transaction_id");
                order.Property(o => o.UserId).HasColumnName("user_id");
                order.Property(o => o.BookId).HasColumnName("book_id");
                order.Property(o => o.BookName).HasColumnName("book_name");
                order.Property(o => o.Quantity).HasColumnName("quantity");
                order.Property(o => o.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").HasConversion<decimal>();
                order.Property(o => o.Status).HasColumnName("status").HasConversion<string>();
                order.Property(o => o.Message).HasColumnName("message");
                order.Property(o => o.CreatedUtc).HasColumnName("created_utc");
                order.Property(o => o.UpdatedUtc).HasColumnName("updated_utc");
            });
        }
    }
}
