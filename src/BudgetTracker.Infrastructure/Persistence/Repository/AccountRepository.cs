using Dapper;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using System.Data;

namespace BudgetTracker.Infrastructure.Persistence.Repository;

public class AccountRepository : RepositoryBase<Account>, IAccountRepository
{
    public AccountRepository(DbSession connection) : base(connection)
    {
    }
    public async Task<Account?> GetByEmailAsync(string email)
    {
        var query = @"SELECT * FROM Account WHERE Email = @Email";

        await _db.OpenAsync();

        var acount = await _db._connection.QueryFirstOrDefaultAsync<Account>(query, new { Email = email }, _db._transaction);
        return acount;
    }

    public async Task<Account?> GetByToken(string token)
    {
        var query = @"SELECT * FROM Account WHERE EmailVerificationToken = @EmailVerificationToken";

        await _db.OpenAsync();

        var account = await _db._connection.QueryFirstOrDefaultAsync<Account>(query, new { EmailVerificationToken = token }, _db._transaction);

        return account;
    }

    public async Task UpdateBalanceAtomicAsync(long accountId, decimal delta)
    {
        await _db.OpenAsync();

        const string query = @"UPDATE ""Account"" SET ""Balance"" = ""Balance"" + @Delta WHERE ""Id"" = @AccountId";
        await _db._connection.ExecuteAsync(query, new { Delta = delta, AccountId = accountId }, _db._transaction);
    }
}

