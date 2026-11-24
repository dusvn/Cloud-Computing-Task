using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace BookstoreService.Database
{
    /// <summary>Every database READ on public.books (LINQ only).</summary>
    internal sealed class GetBookQueries
    {
        private readonly string _connectionString;

        public GetBookQueries(string connectionString) => _connectionString = connectionString;

        public async Task<List<Book>> GetBooksAsync(int take, CancellationToken ct = default)
        {
            await using var db = new BookstoreDbContext(_connectionString);
            return await db.Books.AsNoTracking().Take(take).ToListAsync(ct);
        }
    }
}
