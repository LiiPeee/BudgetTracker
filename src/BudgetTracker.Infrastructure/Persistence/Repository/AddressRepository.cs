using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using System.Data;
using Dapper;

namespace BudgetTracker.Infrastructure.Persistence.Repository;

public class AddressRepository : AccountScopedRepositoryBase<Address>, IAddressRepository
{
    public AddressRepository(DbSession connection) : base(connection)
    {
    }

    // O principal primeiro: é o que a tela mostra e edita.
    public async Task<IEnumerable<Address>> GetByAccountAsync(long accountId)
    {
        const string query = "SELECT * FROM Address WHERE AccountId = @AccountId ORDER BY ContactId, IsPrimary DESC, Id";

        await EnsureConnectionOpenAsync();

        return await _db._connection.QueryAsync<Address>(query, new { AccountId = accountId }, _db._transaction);
    }
}


