using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Models.Output;
using BudgetTracker.Core.Domain.Models.Request.Account;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.Service;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Domain.Utils;
using BudgetTracker.Core.Infrastructure.Repository;
using BudgetTracker.Core.Infrastructure.Services;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
namespace BudgetTracker.Application.Service;

public class AuthenticationAppService(IAccountRepository accountRepository,
    IResetPasswordRepository resetPasswordRepository,
    IPasswordHelper passwordHelper,
    IPasswordHasher passwordHasher,
    IEmailService emailService,
    IUnitOfWork unitOfWork,
    IConfiguration configuration) : IAuthenticationAppService
{
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IEmailService _emailService = emailService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IPasswordHelper _passwordHelper = passwordHelper;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;

    private readonly IResetPasswordRepository _resetPasswordRepository = resetPasswordRepository;

    private static readonly string DummyPasswordHash = new PasswordHasherService().Hash("timing-equalizer-not-a-real-password");

    public async Task<string?> SignUpAsync(CreateAccountRequestDto request)
    {
        var existingAccount = await _accountRepository.GetByEmailAsync(request.Email);

        if (existingAccount is not null)
            return "We send a verification email for you";

        if (string.IsNullOrWhiteSpace(request.Password)) throw new ArgumentException("password must not be empty");

        if (request.Password.Length < 8) throw new ArgumentException("password must be at least 8 characters");

        if (request.Password.Any(char.IsUpper) == false) throw new ArgumentException("password must contain at least one uppercase letter");

        if (request.Password.Any(char.IsLower) == false) throw new ArgumentException("password must contain at least one lowercase letter");

        var hashPassword = _passwordHasher.Hash(request.Password);

        // O token em claro é enviado apenas por e-mail; no banco fica o hash SHA-256.
        var verificationToken = _passwordHelper.GenerateVerificationCode();

        var account = new Account
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Password = hashPassword,
            Balance = 0,
            IsActive = false,
            EmailVerified = false,
            VerifiedAt = null,
            EmailVerificationToken = HashResetCode(verificationToken),
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(4)
        };

        try
        {
            _unitOfWork.BeginTransaction();
            var savedAccount = await _accountRepository.AddAsync(account);

            var idEncrypted = _passwordHelper.EncryptUrl(savedAccount.Id.ToString());
            await _emailService.SendCodeToEmailAsync(account.Email, idEncrypted, verificationToken);

            _unitOfWork.Commit();

            return "We send a verification email for you";
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }
    public async Task<string?> VerifyTokenSignUpAsync(VerifyTokenRequestDto request)
    {
        try
        {
            var account = await _accountRepository.GetByIdAsync(long.Parse(_passwordHelper.DecryptUrl(request.Id)));

            if (account is null || account.VerifyAttempts > 5)
            {
                throw new ArgumentException("Exceeds attempts");
            }

            if (!IsEmailTokenValid(account.EmailVerificationToken, request.Token))
            {
                account.VerifyAttempts += 1;
                await PersistAccountAsync(account);
                throw new ArgumentException("Invalid Token");
            }

            if (account.EmailVerificationTokenExpiry < DateTime.UtcNow)
            {
                account.VerifyAttempts += 1;
                await PersistAccountAsync(account);
                throw new ArgumentException("Token Expiry");
            }

            account.EmailVerified = true;
            account.EmailVerificationToken = null;
            account.VerifiedAt = DateTime.UtcNow;
            account.IsActive = true;
            account.VerifyAttempts = 0;

            await PersistAccountAsync(account);

            return "Your email has been verified successfully";
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    public async Task<string?> ValidateResetCodeAsync(string email, string token)
    {
        var account = await _accountRepository.GetByEmailAsync(email);

        if (account is null) throw new ArgumentException("Account not found");

        var resetPassword = await _resetPasswordRepository.GetByAccountIdAsync(account.Id);

        if (!IsResetCodeValid(resetPassword, token))
            throw new UnauthorizedAccessException("Invalid or expired reset code");

        return "Token Validated";
    }

    private static bool IsResetCodeValid(ResetPassword? resetPassword, string providedCode)
    {
        if (resetPassword is null
            || string.IsNullOrEmpty(resetPassword.HashedToken)
            || resetPassword.ExpireAt < DateTime.UtcNow)
        {
            return false;
        }

        // Stored value is the hash of the code (the plaintext was only ever emailed); compare hashes.
        var stored = Encoding.UTF8.GetBytes(resetPassword.HashedToken);
        var provided = Encoding.UTF8.GetBytes(HashResetCode(providedCode ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(stored, provided);
    }

    private static bool IsEmailTokenValid(string? storedHash, string? providedCode)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(providedCode)) return false;

        var stored = Encoding.UTF8.GetBytes(storedHash);
        var provided = Encoding.UTF8.GetBytes(HashResetCode(providedCode));
        return CryptographicOperations.FixedTimeEquals(stored, provided);
    }

    private static string HashResetCode(string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private async Task PersistAccountAsync(Account account)
    {
        _unitOfWork.BeginTransaction();
        await _accountRepository.UpdateAsync(account);
        _unitOfWork.Commit();
    }
    public async Task<string?> VerifyEmailAsync(string email)
    {
        try
        {
            var account = await _accountRepository.GetByEmailAsync(email);
            if (account is null) throw new ArgumentException();

            string token = _passwordHelper.GenerateVerificationCode();

            var resetPassword = new ResetPassword()
            {
                AccountId = account.Id,
                HashedToken = HashResetCode(token),
                ExpireAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow,
            };

            var reset = await _resetPasswordRepository.GetByAccountIdAsync(account.Id);

            if (reset is not null)
            {
                reset.ExpireAt = resetPassword.ExpireAt;
                reset.HashedToken = resetPassword.HashedToken;

                _unitOfWork.BeginTransaction();

                await _resetPasswordRepository.UpdateAsync(reset);

                await _emailService.SendVerificationEmailAsync(email, token);

                _unitOfWork.Commit();

                return "Reset email sended";
            }
            else
            {
                _unitOfWork.BeginTransaction();

                await _resetPasswordRepository.AddAsync(resetPassword);

                await _emailService.SendVerificationEmailAsync(email, token);

                _unitOfWork.Commit();

                return "Reset email sended";
            }
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    public async Task<string?> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        try
        {
            var account = await _accountRepository.GetByEmailAsync(request.email);

            if (account is null) throw new KeyNotFoundException("Account not found");

            var resetPassword = await _resetPasswordRepository.GetByAccountIdAsync(account.Id);

            if (!IsResetCodeValid(resetPassword, request.token))
                throw new UnauthorizedAccessException("Invalid or expired reset code");

            account.Password = _passwordHasher.Hash(request.newPassword);

            // Invalidate the code so it cannot be replayed, atomically with the password change.
            resetPassword!.HashedToken = null;
            resetPassword.ExpireAt = DateTime.UtcNow;

            _unitOfWork.BeginTransaction();

            await _accountRepository.UpdateAsync(account);
            await _resetPasswordRepository.UpdateAsync(resetPassword);

            _unitOfWork.Commit();

            return "Password Reseted";
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    public async Task<TokenResponseDto?> SignInAsync(LoginRequestDto request)
    {
        try
        {
            var account = await _accountRepository.GetByEmailAsync(request.Email);

            if (account is null)
            {
                _passwordHasher.Verify(DummyPasswordHash, request.Password, out _);
                throw new UnauthorizedAccessException("invalid credentials");
            }

            if (account.VerifyAttempts > 5) throw new UnauthorizedAccessException("exceeds attempts");

            if (account.LoginAttempts >= 5) throw new UnauthorizedAccessException("account temporarily locked due to too many failed login attempts");

            if (!_passwordHasher.Verify(account.Password, request.Password, out var rehashNeeded))
            {
                account.LoginAttempts += 1;
                await _accountRepository.UpdateAsync(account);
                throw new UnauthorizedAccessException("invalid credentials");
            }

            var hadFailedAttempts = account.LoginAttempts > 0;
            account.LoginAttempts = 0;

            if (rehashNeeded)
            {
                // Upgrade on login: re-hash com o algoritmo atual (PBKDF2-HMACSHA512/100k)
                // sem forçar o usuário a redefinir a senha.
                account.Password = _passwordHasher.Hash(request.Password);
                await _accountRepository.UpdateAsync(account);
            }
            else if (hadFailedAttempts)
            {
                await _accountRepository.UpdateAsync(account);
            }

            if (account.IsActive == false) throw new UnauthorizedAccessException("account is not active");

            if (account.EmailVerified == false) throw new UnauthorizedAccessException("email is not verified");

            return new TokenResponseDto()
            {
                AccessToken = CreateToken(account),
                RefreshToken = await GenerateAndSaveRefreshToken(account)
            };
        }
        catch
        {
            throw;
        }
    }

    public async Task<string> LogOutAsync(long accountId)
    {
        try
        {
            var account = await _accountRepository.GetByIdAsync(accountId);

            if (account is null)
                throw new Exception("account not found");

            account.RefreshToken = null;
            account.RefreshTokenExpiryTime = null;

            _unitOfWork.BeginTransaction();
            await _accountRepository.UpdateAsync(account);
            _unitOfWork.Commit();

            return "logged out successfully";
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    public async Task<TokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        var account = await _accountRepository.GetByIdAsync(request.AccountId);

        if (account is null) throw new Exception("invalid refresh token");

        if (account.RefreshToken != request.RefreshToken || account.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            // Invalida o token mesmo quando rejeitado, para evitar reuso
            try
            {
                account.RefreshToken = null;
                account.RefreshTokenExpiryTime = null;
                _unitOfWork.BeginTransaction();
                await _accountRepository.UpdateAsync(account);
                _unitOfWork.Commit();
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
            throw new Exception("invalid refresh token");
        }

        // Token válido — rotaciona (limpa o atual, gera um novo)
        try
        {
            account.RefreshToken = null;
            account.RefreshTokenExpiryTime = null;
            _unitOfWork.BeginTransaction();
            await _accountRepository.UpdateAsync(account);
            _unitOfWork.Commit();
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }

        return new TokenResponseDto()
        {
            AccessToken = CreateToken(account),
            RefreshToken = await GenerateAndSaveRefreshToken(account)
        };
    }

    private string CreateToken(Account account)
    {
        var claims = new List<Claim>
            {
                new(ClaimTypes.Name, account.Email),
                new(ClaimTypes.NameIdentifier, account.Id.ToString()),
                new(ClaimTypes.Role, account.Role)
            };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration.GetValue<string>("Jwt:Token")));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

        var tokenDescripton = new JwtSecurityToken(
            issuer: configuration.GetValue<string>("Jwt:Issuer"),
            audience: configuration.GetValue<string>("Jwt:Audience"),
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:TokenExpirationMinutes")),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(tokenDescripton);
    }

    private async Task<string> GenerateAndSaveRefreshToken(Account account)
    {
        try
        {
            var refreshToken = _passwordHelper.GenerateRefreshToken();

            account.RefreshToken = refreshToken;
            account.RefreshTokenExpiryTime = DateTime.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:RefreshTokenExpirationMinutes"));

            _unitOfWork.BeginTransaction();
            await _accountRepository.UpdateAsync(account);
            _unitOfWork.Commit();

            return refreshToken;
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    public async Task<TokenResponseDto?> SignInGoogleAsync(GoogleLoginRequestDto request)
    {
        var payload = await ValidateGoogleTokenAsync(request.IdToken);

        var account = await _accountRepository.GetByEmailAsync(payload.Email);

        if (account is null)
        {
            account = await CreateAccountFromGoogleAsync(payload);
        }
        else if (!account.IsActive)
        {
            throw new UnauthorizedAccessException("account is not active");
        }

        return new TokenResponseDto
        {
            AccessToken = CreateToken(account),
            RefreshToken = await GenerateAndSaveRefreshToken(account)
        };
    }

    private async Task<GoogleJsonWebSignature.Payload> ValidateGoogleTokenAsync(string idToken)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [configuration.GetValue<string>("Authentication:Google:ClientId")]
        };

        return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
    }

    private async Task<Account> CreateAccountFromGoogleAsync(GoogleJsonWebSignature.Payload payload)
    {
        var account = new Account
        {
            FirstName = payload.GivenName ?? string.Empty,
            LastName = payload.FamilyName ?? string.Empty,
            Email = payload.Email,
            Password = null,
            Balance = 0,
            Role = "User",
            IsActive = true,
            EmailVerified = payload.EmailVerified,
            VerifiedAt = payload.EmailVerified ? DateTime.UtcNow : null
        };

        try
        {
            _unitOfWork.BeginTransaction();
            var saved = await _accountRepository.AddAsync(account);
            _unitOfWork.Commit();
            return saved;
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }
}


