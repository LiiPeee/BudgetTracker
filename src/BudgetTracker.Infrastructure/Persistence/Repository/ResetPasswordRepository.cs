
using System.Data;
using Dapper;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Infrastructure.Repository;

namespace BudgetTracker.Infrastructure.Persistence.Repository
{
    public class ResetPasswordRepository : AccountScopedRepositoryBase<ResetPassword>, IResetPasswordRepository
    {
        public ResetPasswordRepository(DbSession connection) : base(connection)
        {
        }

       public async Task<ResetPassword?> GetByAccountIdAsync(long accountId)
        {
            var query = @"SELECT * FROM ResetPassword WHERE AccountId = @AccountId";

            var parameters = new { AccountId = accountId };

            await _db.OpenAsync();

            var result = await _db._connection.QueryFirstOrDefaultAsync<ResetPassword>(query, parameters);

            return result;
        }
    }
}


