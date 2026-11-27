using System;
using BudgetTracker.Core.Domain.Entities;

namespace BudgetTracker.Core.Domain.Repository;

public interface IAddressRepository : IAccountScopedRepository<Address>
{
    Task<IEnumerable<Address>> GetByAccountAsync(long accountId);
}


