using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace BankService
{
    /// <summary>LINQ access to the Postgres table public.users.</summary>
    internal sealed class BankDbContext : DbContext
    {
        private readonly string _connectionString;

        public BankDbContext(string connectionString) => _connectionString = connectionString;

        public DbSet<User> Users => Set<User>();

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseNpgsql(_connectionString);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(user =>
            {
                user.ToTable("users", "public");
                user.HasKey(u => u.UserId);
                user.Property(u => u.UserId).HasColumnName("user_id");
                user.Property(u => u.FirstName).HasColumnName("first_name");
                user.Property(u => u.LastName).HasColumnName("last_name");
                user.Property(u => u.Balance).HasColumnName("balance").HasColumnType("numeric(12,2)").HasConversion<decimal>();
                user.Property(u => u.Email).HasColumnName("email");
            });
        }
    }
}
