using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace BookstoreService.Database
{
    /// <summary>Every database WRITE on public.books (LINQ only). Absolute values, so a retry is harmless.</summary>
    internal sealed class PostBookQueries
    {
        private readonly string _connectionString;

        public PostBookQueries(string connectionString) => _connectionString = connectionString;

        public async Task UpdateBookQuantityAsync(Guid bookId, int quantity, CancellationToken ct = default)
        {
            await using var db = new BookstoreDbContext(_connectionString);
            await db.Books
                .Where(b => b.BookId == bookId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Quantity, quantity), ct);
        }
    }
}
