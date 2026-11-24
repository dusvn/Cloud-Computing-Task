using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace BankService.Database
{
    /// <summary>Every database READ on public.users (LINQ only).</summary>
    internal sealed class GetUserQueries
    {
        private readonly string _connectionString;

        public GetUserQueries(string connectionString) => _connectionString = connectionString;

        public async Task<List<User>> GetUsersAsync(int take, CancellationToken ct = default)
        {
            await using var db = new BankDbContext(_connectionString);
            return await db.Users.AsNoTracking().Take(take).ToListAsync(ct);
        }
    }
}
