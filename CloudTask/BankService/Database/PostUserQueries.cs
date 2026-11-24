using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace BankService.Database
{
    /// <summary>Every database WRITE on public.users (LINQ only). Absolute values, so a retry is harmless.</summary>
    internal sealed class PostUserQueries
    {
        private readonly string _connectionString;

        public PostUserQueries(string connectionString) => _connectionString = connectionString;

        public async Task UpdateUserBalanceAsync(Guid userId, double balance, CancellationToken ct = default)
        {
            await using var db = new BankDbContext(_connectionString);
            await db.Users
                .Where(u => u.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Balance, balance), ct);
        }
    }
}
