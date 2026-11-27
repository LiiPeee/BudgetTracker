using BudgetTracker.Core.Domain.Entities;

namespace BudgetTracker.Core.Domain.Repository;

public interface IAccountRepository : IRepositoryBase<Account>
{
    Task<Account?> GetByEmailAsync(string email);
    Task<Account?> GetByToken(string token);
    Task UpdateBalanceAtomicAsync(long accountId, decimal delta);
    Task RevokeRefreshTokenAsync(long accountId);
}


