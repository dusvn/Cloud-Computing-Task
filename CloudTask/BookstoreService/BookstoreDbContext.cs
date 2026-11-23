using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace BookstoreService
{
    /// <summary>LINQ access to the Postgres table public.books.</summary>
    internal sealed class BookstoreDbContext : DbContext
    {
        private readonly string _connectionString;

        public BookstoreDbContext(string connectionString) => _connectionString = connectionString;

        public DbSet<Book> Books => Set<Book>();

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseNpgsql(_connectionString);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Book>(book =>
            {
                book.ToTable("books", "public");
                book.HasKey(b => b.BookId);
                book.Property(b => b.BookId).HasColumnName("book_id");
                book.Property(b => b.BookName).HasColumnName("book_name");
                book.Property(b => b.Quantity).HasColumnName("quantity");
                book.Property(b => b.Price).HasColumnName("price").HasColumnType("numeric(10,2)").HasConversion<decimal>();
            });
        }
    }
}
