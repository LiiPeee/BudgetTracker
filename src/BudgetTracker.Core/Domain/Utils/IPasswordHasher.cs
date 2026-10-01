namespace BudgetTracker.Core.Domain.Utils;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hashedPassword, string providedPassword, out bool rehashNeeded);
}
