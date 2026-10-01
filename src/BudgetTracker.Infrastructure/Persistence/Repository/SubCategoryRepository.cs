using Dapper;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetTracker.Infrastructure.Persistence.Repository
{
    public class SubCategoryRepository : AccountScopedRepositoryBase<SubCategory>, ISubCategoryRepository
    {
        public SubCategoryRepository(DbSession connection) : base(connection)
        {
        }


        public async Task<SubCategory?> GetByNameAsync(long accountId, string name, long? categoryId)
        {
            var query = @"SELECT * FROM SubCategory WHERE Name = @Name AND AccountId = @AccountId AND CategoryId = @CategoryId ORDER BY Id LIMIT 1";

            await _db.OpenAsync();

            return await _db._connection.QueryFirstOrDefaultAsync<SubCategory>(query, new { Name = name, AccountId = accountId, CategoryId = categoryId }, transaction: _db._transaction);
        }
    }
}


