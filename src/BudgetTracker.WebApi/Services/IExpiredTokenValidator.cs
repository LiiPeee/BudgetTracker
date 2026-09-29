using System.Security.Claims;

namespace BudgetTracker.WebApi.Services;

public interface IExpiredTokenValidator
{
    long? GetAccountIdFromExpiredToken(string? authorizationHeader);
}
