using Dapper;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using System.Data;

namespace BudgetTracker.Infrastructure.Persistence.Repository;

public class ContactRepository : AccountScopedRepositoryBase<Contact>, IContactRepository
{
    public ContactRepository(DbSession connection) : base(connection)
    {
    }

    public async Task<Contact?> GetByNameAsync(long accountId, string name)
    {
        var query = "SELECT * FROM Contact WHERE Name = @Name AND AccountId = @AccountId AND IsActive = true ORDER BY Id LIMIT 1";

        await _db.OpenAsync();

        return await _db._connection.QueryFirstOrDefaultAsync<Contact>(query, new { Name = name, AccountId = accountId }, transaction: _db._transaction);
    }
    
    public async Task<List<Contact?>> GetByIdAccount(long accountId)
    {
        var query = @"SELECT * FROM Contact WHERE AccountId = @AccountId AND IsActive = true";

        await _db.OpenAsync();

        return (await _db._connection.QueryAsync<Contact>(query, new { AccountId = accountId }, transaction: _db._transaction)).ToList();
    }
}


