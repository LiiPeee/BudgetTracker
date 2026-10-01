using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Infrastructure.Repository;
using Dapper;

namespace BudgetTracker.Infrastructure.Persistence.Repository
{
    public class StockRepository : AccountScopedRepositoryBase<Stock>, IStockRepository
    {
        public StockRepository(DbSession session) : base(session)
        {
        }

        public async Task<Stock?> GetByStockAndAccountAsync(long accountId, string ticker)
        {
            var query = $"SELECT * FROM {_tableName} WHERE AccountId = @AccountId AND Ticker = @Ticker";
            await EnsureConnectionOpenAsync();
            return await _db._connection.QuerySingleOrDefaultAsync<Stock>(
                query, new { AccountId = accountId, Ticker = ticker }, _db._transaction);
                
        }
    }
}
