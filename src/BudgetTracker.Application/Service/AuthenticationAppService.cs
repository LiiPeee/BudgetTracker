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
using System.Diagnostics.CodeAnalysis;
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
    IConfiguration configuration,
    AuthAttemptTracker attempts) : IAuthenticationAppService
{
    private const int MaxLoginAttempts = 5;
    private const int MaxResetCodeAttempts = 5;
    private const int MaxVerificationResends = 3;
    private const string SignUpMessage = "We send a verification email for you";
    private static readonly TimeSpan LoginLockDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResetCodeWindow = TimeSpan.FromHours(1);
    private const string LockedMessage = "account temporarily locked due to too many failed login attempts";
    private const string InvalidResetCodeMessage = "Invalid or expired reset code";

    private readonly AuthAttemptTracker _attempts = attempts;
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IEmailService _emailService = emailService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IPasswordHelper _passwordHelper = passwordHelper;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;

    private readonly IResetPasswordRepository _resetPasswordRepository = resetPasswordRepository;

    private static readonly string DummyPasswordHash = new PasswordHasherService().Hash("timing-equalizer-not-a-real-password");

    public async Task<string?> SignUpAsync(CreateAccountRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Password)) throw new ArgumentException("password must not be empty");

        if (request.Password.Length < 8) throw new ArgumentException("password must be at least 8 characters");

        if (request.Password.Any(char.IsUpper) == false) throw new ArgumentException("password must contain at least one uppercase letter");

        if (request.Password.Any(char.IsLower) == false) throw new ArgumentException("password must contain at least one lowercase letter");

        var existingAccount = await _accountRepository.GetByEmailAsync(request.Email);

        if (existingAccount is not null)
        {
            if (!existingAccount.EmailVerified)
                await ResendVerificationAsync(existingAccount, request);

            return SignUpMessage;
        }

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

            return SignUpMessage;
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    // Cadastro nunca confirmado (o código vence em 4h): um novo cadastro troca os dados e envia
    // outro código. O limite por conta evita encher a caixa de alguém e testar códigos em série.
    private async Task ResendVerificationAsync(Account account, CreateAccountRequestDto request)
    {
        if (!_attempts.TryRegisterAttempt($"signup-resend:{account.Id}", MaxVerificationResends, ResetCodeWindow))
            return;

        var verificationToken = _passwordHelper.GenerateVerificationCode();

        account.FirstName = request.FirstName;
        account.LastName = request.LastName;
        account.Password = _passwordHasher.Hash(request.Password);
        account.EmailVerificationToken = HashResetCode(verificationToken);
        account.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(4);
        account.VerifyAttempts = 0;

        try
        {
            _unitOfWork.BeginTransaction();
            await _accountRepository.UpdateAsync(account);
            await _emailService.SendCodeToEmailAsync(account.Email, _passwordHelper.EncryptUrl(account.Id.ToString()), verificationToken);
            _unitOfWork.Commit();
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

        await RequireValidResetCodeAsync(account, token);

        return "Token Validated";
    }

    // Mesma mensagem para conta inexistente, código errado e limite excedido: não revela cadastro.
    // O limite é por conta (independe do IP) e não zera quando um novo código é solicitado.
    private async Task<ResetPassword> RequireValidResetCodeAsync(Account? account, string token)
    {
        if (account is null) throw new UnauthorizedAccessException(InvalidResetCodeMessage);

        var key = $"reset:{account.Id}";

        if (!_attempts.TryRegisterAttempt(key, MaxResetCodeAttempts, ResetCodeWindow))
            throw new UnauthorizedAccessException(InvalidResetCodeMessage);

        var resetPassword = await _resetPasswordRepository.GetByAccountIdAsync(account.Id);

        if (!IsResetCodeValid(resetPassword, token))
            throw new UnauthorizedAccessException(InvalidResetCodeMessage);

        _attempts.Clear(key);

        return resetPassword!;
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

    private static string HashResetCode(string code) => Sha256Hex(code);

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsRefreshTokenValid(string? storedHash, string? provided)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(provided)) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedHash), Encoding.UTF8.GetBytes(Sha256Hex(provided)));
    }

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
            // Resposta igual à de sucesso para não revelar quais e-mails têm cadastro.
            if (account is null) return "Reset email sended";

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

            var resetPassword = await RequireValidResetCodeAsync(account, request.token);

            account!.Password = _passwordHasher.Hash(request.newPassword);
            account.LoginAttempts = 0;

            // O código recebido por e-mail prova a posse do endereço: conclui o cadastro pendente.
            if (!account.EmailVerified)
            {
                account.EmailVerified = true;
                account.IsActive = true;
                account.VerifiedAt = DateTime.UtcNow;
                account.VerifyAttempts = 0;
                account.EmailVerificationTokenExpiry = DateTime.UtcNow;
            }

            // Invalidate the code so it cannot be replayed, atomically with the password change.
            resetPassword.HashedToken = null!;
            resetPassword.ExpireAt = DateTime.UtcNow;

            _unitOfWork.BeginTransaction();

            await _accountRepository.UpdateAsync(account);
            await _resetPasswordRepository.UpdateAsync(resetPassword);
            await _accountRepository.RevokeRefreshTokenAsync(account.Id);

            _unitOfWork.Commit();
            _attempts.Clear(LoginKey(account.Id));

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
                RejectUnknownEmail(request);
            }

            if (account.LoginAttempts >= MaxLoginAttempts)
            {
                if (_attempts.IsLocked(LoginKey(account.Id)))
                    throw new UnauthorizedAccessException(LockedMessage);

                // O bloqueio venceu: recomeça a contagem.
                account.LoginAttempts = 0;
                await _accountRepository.UpdateAsync(account);
            }

            // Contas criadas pelo Google não têm senha.
            if (string.IsNullOrEmpty(account.Password))
            {
                _passwordHasher.Verify(DummyPasswordHash, request.Password, out _);
                throw new UnauthorizedAccessException("invalid credentials");
            }

            if (!_passwordHasher.Verify(account.Password, request.Password, out var rehashNeeded))
            {
                account.LoginAttempts += 1;
                await _accountRepository.UpdateAsync(account);

                if (account.LoginAttempts >= MaxLoginAttempts)
                    _attempts.Lock(LoginKey(account.Id), LoginLockDuration);
                throw new UnauthorizedAccessException("invalid credentials");
            }

            // Só depois de conferir a senha: antes disso a mensagem revelaria que a conta existe.
            if (account.VerifyAttempts > 5) throw new UnauthorizedAccessException("exceeds attempts");

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

            _unitOfWork.BeginTransaction();
            await _accountRepository.RevokeRefreshTokenAsync(account.Id);
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

        if (account is null) throw new UnauthorizedAccessException("invalid refresh token");

        if (!account.IsActive || !IsRefreshTokenValid(account.RefreshToken, request.RefreshToken) || account.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            // Invalida o token mesmo quando rejeitado, para evitar reuso
            try
            {
                _unitOfWork.BeginTransaction();
                await _accountRepository.RevokeRefreshTokenAsync(account.Id);
                _unitOfWork.Commit();
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
            throw new UnauthorizedAccessException("invalid refresh token");
        }

        // Token válido — rotaciona: o novo sobrescreve o atual.
        return new TokenResponseDto()
        {
            AccessToken = CreateToken(account),
            RefreshToken = await GenerateAndSaveRefreshToken(account)
        };
    }

    // E-mail sem cadastro é bloqueado como um cadastrado, para o bloqueio não revelar quem existe.
    [DoesNotReturn]
    private void RejectUnknownEmail(LoginRequestDto request)
    {
        var key = $"login-unknown:{request.Email.ToLowerInvariant()}";

        if (_attempts.IsLocked($"{key}:lock"))
            throw new UnauthorizedAccessException(LockedMessage);

        _passwordHasher.Verify(DummyPasswordHash, request.Password, out _);

        if (!_attempts.TryRegisterAttempt(key, MaxLoginAttempts - 1, LoginLockDuration))
        {
            _attempts.Lock($"{key}:lock", LoginLockDuration);
            _attempts.Clear(key);
        }

        throw new UnauthorizedAccessException("invalid credentials");
    }

    private static string LoginKey(long accountId) => $"login:{accountId}";

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

            // No banco fica só o hash; o valor em claro vai apenas para o cliente.
            account.RefreshToken = Sha256Hex(refreshToken);
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

        if (!payload.EmailVerified)
            throw new UnauthorizedAccessException("email is not verified");

        var account = await _accountRepository.GetByEmailAsync(payload.Email);

        if (account is null)
        {
            account = await CreateAccountFromGoogleAsync(payload);
        }
        else if (!account.EmailVerified)
        {
            await ClaimUnverifiedAccountAsync(account);
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

    // Cadastro nunca confirmado com este e-mail: o Google provou quem é o dono, então a conta passa
    // para ele. A senha definida no cadastro (que pode ser de outra pessoa) deixa de valer.
    private async Task ClaimUnverifiedAccountAsync(Account account)
    {
        account.Password = _passwordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        account.EmailVerified = true;
        account.IsActive = true;
        account.VerifiedAt = DateTime.UtcNow;
        account.LoginAttempts = 0;
        account.VerifyAttempts = 0;

        try
        {
            _unitOfWork.BeginTransaction();
            await _accountRepository.UpdateAsync(account);
            await _accountRepository.RevokeRefreshTokenAsync(account.Id);
            _unitOfWork.Commit();
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    protected virtual async Task<GoogleJsonWebSignature.Payload> ValidateGoogleTokenAsync(string idToken)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            // Program.cs usa "Google:ClientId"; a chave antiga continua valendo se estiver definida.
            Audience = [configuration["Authentication:Google:ClientId"] ?? configuration["Google:ClientId"]]
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


