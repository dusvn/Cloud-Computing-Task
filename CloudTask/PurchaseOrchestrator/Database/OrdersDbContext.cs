using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PurchaseOrchestrator.Database
{
    /// <summary>
    /// The orchestrator's database connection: public.orders (current state of each order, for reading)
    /// and public.saga_log (append-only saga log, used to resume sagas). Used only by the query classes.
    /// </summary>
    internal sealed class OrdersDbContext : DbContext
    {
        private readonly string _connectionString;

        public OrdersDbContext(string connectionString) => _connectionString = connectionString;

        public DbSet<OrderRecord> Orders => Set<OrderRecord>();
        public DbSet<SagaLogEntry> SagaLog => Set<SagaLogEntry>();

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
                // saga-internal data lives in saga_log, not in orders
                order.Ignore(o => o.Email);
                order.Ignore(o => o.Attempts);
            });

            modelBuilder.Entity<SagaLogEntry>(log =>
            {
                log.ToTable("saga_log", "public");
                log.HasKey(l => l.Id);
                log.Property(l => l.Id).HasColumnName("id").UseIdentityByDefaultColumn();
                log.Property(l => l.IdempotencyKey).HasColumnName("idempotency_key");
                log.Property(l => l.TransactionId).HasColumnName("transaction_id");
                log.Property(l => l.UserId).HasColumnName("user_id");
                log.Property(l => l.BookId).HasColumnName("book_id");
                log.Property(l => l.BookName).HasColumnName("book_name");
                log.Property(l => l.Email).HasColumnName("email");
                log.Property(l => l.Quantity).HasColumnName("quantity");
                log.Property(l => l.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").HasConversion<decimal>();
                log.Property(l => l.State).HasColumnName("state").HasConversion<string>();
                log.Property(l => l.Message).HasColumnName("message");
                log.Property(l => l.Attempts).HasColumnName("attempts");
                log.Property(l => l.CreatedUtc).HasColumnName("created_utc");
                log.Property(l => l.LoggedUtc).HasColumnName("logged_utc");
            });
        }
    }
}
