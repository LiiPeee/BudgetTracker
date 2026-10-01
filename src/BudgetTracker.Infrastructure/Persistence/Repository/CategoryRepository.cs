using Dapper;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using System.Data;

namespace BudgetTracker.Infrastructure.Persistence.Repository;

public class CategoryRepository : RepositoryBase<Category>, ICategoryRepository
{
    public CategoryRepository(DbSession connection) : base(connection)
    {
    }

    public async Task<Category?> GetByNameAsync(string name)
    {
        var query = @"SELECT * FROM Category WHERE Name = @Name ORDER BY Id LIMIT 1";

        await _db.OpenAsync();

        return await _db._connection.QueryFirstOrDefaultAsync<Category>(query, new { Name = name }, transaction: _db._transaction);
    }

    public async Task<List<Category>> GetAllAsync()
    {
        var query = @"SELECT * FROM Category LIMIT 1000";

        await _db.OpenAsync();

        var categories = await _db._connection.QueryAsync<Category>(query, transaction: _db._transaction);
        return categories.ToList();
    }
}


