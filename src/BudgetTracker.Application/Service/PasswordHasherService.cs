using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Utils;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using LegacyPasswordHasher = Microsoft.AspNet.Identity.PasswordHasher;
using LegacyPasswordVerificationResult = Microsoft.AspNet.Identity.PasswordVerificationResult;

namespace BudgetTracker.Application.Service;

/// <summary>
/// Hashing de senha com o algoritmo atual do ASP.NET Core Identity
/// (PBKDF2-HMACSHA512, 100.000 iterações) e fallback transparente para hashes
/// legados do Identity 2.x (PBKDF2-HMACSHA1, 1.000 iterações), sinalizando
/// rehash para que o login faça o upgrade da senha sem forçar redefinição.
/// </summary>
public sealed class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<Account> _currentHasher = new();
    private readonly LegacyPasswordHasher _legacyHasher = new();

    public string Hash(string password) => _currentHasher.HashPassword(null!, password);

    public bool Verify(string hashedPassword, string providedPassword, out bool rehashNeeded)
    {
        rehashNeeded = false;

        try
        {
            var result = _currentHasher.VerifyHashedPassword(null!, hashedPassword, providedPassword);
            if (result == PasswordVerificationResult.Success) return true;
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                rehashNeeded = true;
                return true;
            }
        }
        catch (FormatException)
        {
            // Hash legado (Identity 2.x) não é base64 válido para o formato atual.
        }
        catch (CryptographicException)
        {
            // Hash corrompido — tratado como falha.
        }

        try
        {
            if (_legacyHasher.VerifyHashedPassword(hashedPassword, providedPassword) != LegacyPasswordVerificationResult.Failed)
            {
                rehashNeeded = true;
                return true;
            }
        }
        catch (FormatException)
        {
            // Hash malformado — tratado como falha.
        }
        catch (CryptographicException)
        {
            // Hash corrompido — tratado como falha.
        }

        return false;
    }
}
