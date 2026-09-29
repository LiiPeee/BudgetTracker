using BudgetTracker.Application.Service;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Models.Output;
using BudgetTracker.Core.Domain.Models.Request.Account;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Domain.Utils;
using BudgetTracker.Core.Infrastructure.Repository;
using BudgetTracker.Core.Infrastructure.Services;
using Microsoft.AspNet.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace Test;

public class AuthServiceTest
{
    private Mock<IAccountRepository>       _accountRepo;
    private Mock<IResetPasswordRepository> _resetPasswordRepo;
    private Mock<IPasswordHelper>          _passwordHelper;
    private Mock<IEmailService>            _emailService;
    private Mock<IUnitOfWork>              _unitOfWork;
    private Mock<IConfiguration>           _configuration;
    private AuthenticationAppService       _service;

    // Senha real para testes de SignIn que usam PasswordHasher diretamente
    private const string ValidPassword       = "ValidPass1!";
    private static readonly string HashedValidPassword = new PasswordHasher().HashPassword(ValidPassword);

    // O token de e-mail é armazenado como hash SHA-256 (o texto puro só vai por e-mail).
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    [SetUp]
    public void Setup()
    {
        _accountRepo       = new Mock<IAccountRepository>();
        _resetPasswordRepo = new Mock<IResetPasswordRepository>();
        _passwordHelper    = new Mock<IPasswordHelper>();
        _emailService      = new Mock<IEmailService>();
        _unitOfWork        = new Mock<IUnitOfWork>();
        _configuration     = new Mock<IConfiguration>();

        _service = new AuthenticationAppService(
            _accountRepo.Object,
            _resetPasswordRepo.Object,
            _passwordHelper.Object,
            new PasswordHasherService(),
            _emailService.Object,
            _unitOfWork.Object,
            _configuration.Object);
    }

    // Configura IConfiguration para geração de JWT
    private void SetupJwt()
    {
        SetupConfigSection("Jwt:Token", "0123456789012345678901234567890123456789012345678901234567890123");
        SetupConfigSection("Jwt:Issuer", "test-issuer");
        SetupConfigSection("Jwt:Audience", "test-audience");
        SetupConfigSection("Jwt:TokenExpirationMinutes", "60");
        SetupConfigSection("Jwt:RefreshTokenExpirationMinutes", "60");
    }

    private void SetupConfigSection(string key, string value)
    {
        var section = new Mock<IConfigurationSection>();
        section.Setup(s => s.Value).Returns(value);
        _configuration.Setup(c => c.GetSection(key)).Returns(section.Object);
    }

    private static CreateAccountRequestDto BuildSignUpRequest(string email = "novo@test.com", string password = "ValidPass1!") => new()
    {
        Email     = email,
        FirstName = "Test",
        LastName  = "User",
        Password  = password,
    };

    // Reset codes are stored hashed at rest; the service compares SHA-256 hashes.
    private static string Sha256Hex(string code) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code)));

    private Account BuildActiveAccount(long id = 1) => new()
    {
        Id             = id,
        Email          = "user@test.com",
        FirstName      = "Test",
        LastName       = "User",
        Password       = HashedValidPassword,
        Role           = "User",
        IsActive       = true,
        EmailVerified  = true,
        VerifyAttempts = 0,
    };

    // ── SignUpAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task SignUpAsync_ValidRequest_SendsVerificationEmailAndCommits()
    {
        var request = BuildSignUpRequest();

        var savedAccount = new Account { Id = 1, Email = "novo@test.com", EmailVerificationToken = "token" };

        _accountRepo.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync((Account?)null);
        _accountRepo.Setup(r => r.AddAsync(It.IsAny<Account>())).ReturnsAsync(savedAccount);
        _passwordHelper.Setup(p => p.GenerateVerificationCode()).Returns("123456");
        _passwordHelper.Setup(p => p.EncryptUrl(It.IsAny<string>())).Returns("encrypted-id");

        var result = await _service.SignUpAsync(request);

        Assert.That(result, Is.EqualTo("We send a verification email for you"));
        _unitOfWork.Verify(u => u.BeginTransaction(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
        _emailService.Verify(e => e.SendCodeToEmailAsync(request.Email, "encrypted-id", "123456"), Times.Once);
    }

    [Test]
    public async Task SignUpAsync_EmailAlreadyExists_ThrowsArgumentExceptionAndRollsBack()
    {
        var request = BuildSignUpRequest(email: "existe@test.com");
        _accountRepo.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync(new Account { Id = 1 });

        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.SignUpAsync(request));

        Assert.That(ex!.Message, Does.Contain("already exist"));
        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }

    [Test]
    public async Task SignUpAsync_EmptyPassword_ThrowsArgumentException()
    {
        var request = BuildSignUpRequest(password: "   ");
        _accountRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Account?)null);

        Assert.ThrowsAsync<ArgumentException>(() => _service.SignUpAsync(request));
    }

    [Test]
    public async Task SignUpAsync_PasswordTooShort_ThrowsArgumentException()
    {
        var request = BuildSignUpRequest(password: "Ab1!");
        _accountRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Account?)null);

        Assert.ThrowsAsync<ArgumentException>(() => _service.SignUpAsync(request));
    }

    [Test]
    public async Task SignUpAsync_PasswordTooLong_ThrowsArgumentException()
    {
        var request = BuildSignUpRequest(password: "AbcdefghijKlmno12345!");
        _accountRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Account?)null);

        Assert.ThrowsAsync<ArgumentException>(() => _service.SignUpAsync(request));
    }

    [Test]
    public async Task SignUpAsync_NoUppercaseLetter_ThrowsArgumentException()
    {
        var request = BuildSignUpRequest(password: "validpass1!");
        _accountRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Account?)null);

        Assert.ThrowsAsync<ArgumentException>(() => _service.SignUpAsync(request));
    }

    [Test]
    public async Task SignUpAsync_NoLowercaseLetter_ThrowsArgumentException()
    {
        var request = BuildSignUpRequest(password: "VALIDPASS1!");
        _accountRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Account?)null);

        Assert.ThrowsAsync<ArgumentException>(() => _service.SignUpAsync(request));
    }

    [Test]
    public async Task SignUpAsync_RepositoryThrows_RollsBack()
    {
        var request = BuildSignUpRequest();
        _accountRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Account?)null);
        _accountRepo.Setup(r => r.AddAsync(It.IsAny<Account>())).ThrowsAsync(new Exception("db error"));
        _passwordHelper.Setup(p => p.GenerateVerificationCode()).Returns("code");

        Assert.ThrowsAsync<Exception>(() => _service.SignUpAsync(request));

        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    // ── VerifyTokenAsync ─────────────────────────────────────────────────────

    [Test]
    public async Task VerifyTokenAsync_ValidToken_ActivatesAccountAndCommits()
    {
        var account = new Account
        {
            Id = 1,
            EmailVerificationToken = HashToken("valid-token"),
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1),
            VerifyAttempts = 0,
        };

        _passwordHelper.Setup(p => p.DecryptUrl("encrypted-id")).Returns("1");
        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateAsync(It.IsAny<Account>())).ReturnsAsync(true);

        var result = await _service.VerifyTokenSignUpAsync(new VerifyTokenRequestDto { Id = "encrypted-id", Token = "valid-token" });

        Assert.That(result, Is.EqualTo("Your email has been verified successfully"));
        Assert.That(account.IsActive, Is.True);
        Assert.That(account.EmailVerified, Is.True);
        _unitOfWork.Verify(u => u.BeginTransaction(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Test]
    public async Task VerifyTokenAsync_AccountNotFound_ThrowsAndRollsBack()
    {
        _passwordHelper.Setup(p => p.DecryptUrl(It.IsAny<string>())).Returns("1");
        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Account?)null);

        Assert.ThrowsAsync<ArgumentException>(() =>
            _service.VerifyTokenSignUpAsync(new VerifyTokenRequestDto { Id = "id", Token = "token" }));

        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    [Test]
    public async Task VerifyTokenAsync_ExceedsAttempts_ThrowsAndRollsBack()
    {
        var account = new Account { Id = 1, VerifyAttempts = 6, EmailVerificationToken = "t" };
        _passwordHelper.Setup(p => p.DecryptUrl(It.IsAny<string>())).Returns("1");
        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(account);

        Assert.ThrowsAsync<ArgumentException>(() =>
            _service.VerifyTokenSignUpAsync(new VerifyTokenRequestDto { Id = "id", Token = "t" }));

        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    [Test]
    public async Task VerifyTokenAsync_InvalidToken_ThrowsAndRollsBack()
    {
        var account = new Account { Id = 1, VerifyAttempts = 0, EmailVerificationToken = HashToken("correct-token") };
        _passwordHelper.Setup(p => p.DecryptUrl(It.IsAny<string>())).Returns("1");
        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(account);

        var ex = Assert.ThrowsAsync<ArgumentException>(() =>
            _service.VerifyTokenSignUpAsync(new VerifyTokenRequestDto { Id = "id", Token = "wrong-token" }));

        Assert.That(ex!.Message, Does.Contain("Invalid Token"));
        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    [Test]
    public async Task VerifyTokenAsync_ExpiredToken_ThrowsAndRollsBack()
    {
        var account = new Account
        {
            Id = 1,
            VerifyAttempts = 0,
            EmailVerificationToken = HashToken("token"),
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(-1),
        };

        _passwordHelper.Setup(p => p.DecryptUrl(It.IsAny<string>())).Returns("1");
        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(account);

        var ex = Assert.ThrowsAsync<ArgumentException>(() =>
            _service.VerifyTokenSignUpAsync(new VerifyTokenRequestDto { Id = "id", Token = "token" }));

        Assert.That(ex!.Message, Does.Contain("Expiry"));
        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    [Test]
    public void VerifyTokenAsync_InvalidToken_PersistsIncrementedAttempt()
    {
        var account = new Account { Id = 1, VerifyAttempts = 0, EmailVerificationToken = HashToken("correct-token") };
        _passwordHelper.Setup(p => p.DecryptUrl(It.IsAny<string>())).Returns("1");
        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateAsync(It.IsAny<Account>())).ReturnsAsync(true);

        Assert.ThrowsAsync<ArgumentException>(() =>
            _service.VerifyTokenSignUpAsync(new VerifyTokenRequestDto { Id = "id", Token = "wrong-token" }));

        // The increment must be saved, otherwise the >5 lockout never triggers.
        _accountRepo.Verify(r => r.UpdateAsync(It.Is<Account>(a => a.VerifyAttempts == 1)), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Test]
    public async Task VerifyTokenAsync_ValidToken_ResetsVerifyAttempts()
    {
        var account = new Account
        {
            Id = 1,
            VerifyAttempts = 3,
            EmailVerificationToken = HashToken("valid-token"),
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1),
        };
        _passwordHelper.Setup(p => p.DecryptUrl(It.IsAny<string>())).Returns("1");
        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateAsync(It.IsAny<Account>())).ReturnsAsync(true);

        await _service.VerifyTokenSignUpAsync(new VerifyTokenRequestDto { Id = "id", Token = "valid-token" });

        Assert.That(account.VerifyAttempts, Is.EqualTo(0));
    }

    // ── VerifyEmailAsync ─────────────────────────────────────────────────────

    [Test]
    public async Task VerifyEmailAsync_ValidEmail_SendsResetEmailAndCommits()
    {
        var account = new Account { Id = 1, Email = "user@test.com" };
        _accountRepo.Setup(r => r.GetByEmailAsync("user@test.com")).ReturnsAsync(account);
        _passwordHelper.Setup(p => p.GenerateVerificationCode()).Returns("reset-code");
        _resetPasswordRepo.Setup(r => r.AddAsync(It.IsAny<ResetPassword>())).ReturnsAsync(new ResetPassword { Id = 1 });

        var result = await _service.VerifyEmailAsync("user@test.com");

        Assert.That(result, Is.EqualTo("Reset email sended"));
        _unitOfWork.Verify(u => u.BeginTransaction(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
        _emailService.Verify(e => e.SendVerificationEmailAsync("user@test.com", "reset-code"), Times.Once);
    }

    [Test]
    public async Task VerifyEmailAsync_AccountNotFound_ThrowsAndRollsBack()
    {
        _accountRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Account?)null);

        Assert.ThrowsAsync<ArgumentException>(() => _service.VerifyEmailAsync("naoexiste@test.com"));

        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    [Test]
    public async Task VerifyEmailAsync_RepositoryThrows_RollsBack()
    {
        var account = new Account { Id = 1, Email = "user@test.com" };
        _accountRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(account);
        _passwordHelper.Setup(p => p.GenerateVerificationCode()).Returns("reset-code");
        _resetPasswordRepo.Setup(r => r.AddAsync(It.IsAny<ResetPassword>())).ThrowsAsync(new Exception("db error"));

        Assert.ThrowsAsync<Exception>(() => _service.VerifyEmailAsync("user@test.com"));

        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    // ── ResetPasswordAsync ───────────────────────────────────────────────────

    [Test]
    public async Task ResetPasswordAsync_ValidCode_UpdatesPasswordInvalidatesCodeAndCommits()
    {
        var account       = new Account { Id = 1, Email = "user@test.com" };
        var resetPassword = new ResetPassword { Id = 1, AccountId = 1, HashedToken = Sha256Hex("123456"), ExpireAt = DateTime.UtcNow.AddMinutes(30) };

        _accountRepo.Setup(r => r.GetByEmailAsync("user@test.com")).ReturnsAsync(account);
        _resetPasswordRepo.Setup(r => r.GetByAccountIdAsync(1)).ReturnsAsync(resetPassword);
        _accountRepo.Setup(r => r.UpdateAsync(It.IsAny<Account>())).ReturnsAsync(true);
        _resetPasswordRepo.Setup(r => r.UpdateAsync(It.IsAny<ResetPassword>())).ReturnsAsync(true);

        var result = await _service.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            email       = "user@test.com",
            newPassword = "NewValidPass1!",
            token       = "123456"
        });

        Assert.That(result, Is.EqualTo("Password Reseted"));
        Assert.That(resetPassword.HashedToken, Is.Null, "reset code must be invalidated to prevent replay");
        _resetPasswordRepo.Verify(r => r.UpdateAsync(It.IsAny<ResetPassword>()), Times.Once);
        _unitOfWork.Verify(u => u.BeginTransaction(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Test]
    public void ResetPasswordAsync_AccountNotFound_ThrowsAndRollsBack()
    {
        _accountRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Account?)null);

        Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ResetPasswordAsync(new ResetPasswordRequestDto { email = "x@test.com", newPassword = "y", token = "123456" }));

        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    [Test]
    public void ResetPasswordAsync_NoResetCodeRequested_ThrowsUnauthorizedAndDoesNotChangePassword()
    {
        var account = new Account { Id = 1, Email = "user@test.com" };
        _accountRepo.Setup(r => r.GetByEmailAsync("user@test.com")).ReturnsAsync(account);
        _resetPasswordRepo.Setup(r => r.GetByAccountIdAsync(1)).ReturnsAsync((ResetPassword?)null);

        Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ResetPasswordAsync(new ResetPasswordRequestDto { email = "user@test.com", newPassword = "y", token = "123456" }));

        _accountRepo.Verify(r => r.UpdateAsync(It.IsAny<Account>()), Times.Never);
        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    [Test]
    public void ResetPasswordAsync_WrongCode_ThrowsUnauthorizedAndDoesNotChangePassword()
    {
        var account       = new Account { Id = 1, Email = "user@test.com" };
        var resetPassword = new ResetPassword { HashedToken = Sha256Hex("123456"), ExpireAt = DateTime.UtcNow.AddMinutes(30) };

        _accountRepo.Setup(r => r.GetByEmailAsync("user@test.com")).ReturnsAsync(account);
        _resetPasswordRepo.Setup(r => r.GetByAccountIdAsync(1)).ReturnsAsync(resetPassword);

        Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ResetPasswordAsync(new ResetPasswordRequestDto { email = "user@test.com", newPassword = "y", token = "000000" }));

        _accountRepo.Verify(r => r.UpdateAsync(It.IsAny<Account>()), Times.Never);
        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    [Test]
    public void ResetPasswordAsync_ExpiredResetCode_ThrowsUnauthorizedAndRollsBack()
    {
        var account       = new Account { Id = 1, Email = "user@test.com" };
        var resetPassword = new ResetPassword { HashedToken = Sha256Hex("123456"), ExpireAt = DateTime.UtcNow.AddHours(-1) };

        _accountRepo.Setup(r => r.GetByEmailAsync("user@test.com")).ReturnsAsync(account);
        _resetPasswordRepo.Setup(r => r.GetByAccountIdAsync(1)).ReturnsAsync(resetPassword);

        Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ResetPasswordAsync(new ResetPasswordRequestDto { email = "user@test.com", newPassword = "y", token = "123456" }));

        _accountRepo.Verify(r => r.UpdateAsync(It.IsAny<Account>()), Times.Never);
        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    // ── SignInAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task SignInAsync_ValidCredentials_ReturnsTokens()
    {
        SetupJwt();
        var account = BuildActiveAccount();
        _accountRepo.Setup(r => r.GetByEmailAsync(account.Email)).ReturnsAsync(account);
        _passwordHelper.Setup(p => p.GenerateRefreshToken()).Returns("refresh-token");
        _accountRepo.Setup(r => r.UpdateAsync(It.IsAny<Account>())).ReturnsAsync(true);

        var result = await _service.SignInAsync(new LoginRequestDto { Email = account.Email, Password = ValidPassword });

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.AccessToken, Is.Not.Empty);
        Assert.That(result.RefreshToken, Is.EqualTo("refresh-token"));
    }

    [Test]
    public async Task SignInAsync_EmailNotFound_Throws()
    {
        _accountRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Account?)null);

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SignInAsync(new LoginRequestDto { Email = "x@test.com", Password = "any" }));

        // Generic message — must not reveal whether the email exists.
        Assert.That(ex!.Message, Does.Contain("invalid credentials"));
    }

    [Test]
    public async Task SignInAsync_ExceedsAttempts_Throws()
    {
        var account = BuildActiveAccount();
        account.VerifyAttempts = 6;
        _accountRepo.Setup(r => r.GetByEmailAsync(account.Email)).ReturnsAsync(account);

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SignInAsync(new LoginRequestDto { Email = account.Email, Password = ValidPassword }));

        Assert.That(ex!.Message, Does.Contain("exceeds attempts"));
    }

    [Test]
    public async Task SignInAsync_WrongPassword_Throws()
    {
        var account = BuildActiveAccount();
        _accountRepo.Setup(r => r.GetByEmailAsync(account.Email)).ReturnsAsync(account);

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SignInAsync(new LoginRequestDto { Email = account.Email, Password = "WrongPass1!" }));

        // Generic message — must be identical to the "email not found" case.
        Assert.That(ex!.Message, Does.Contain("invalid credentials"));
    }

    [Test]
    public async Task SignInAsync_AccountNotActive_Throws()
    {
        var account = BuildActiveAccount();
        account.IsActive = false;
        _accountRepo.Setup(r => r.GetByEmailAsync(account.Email)).ReturnsAsync(account);

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SignInAsync(new LoginRequestDto { Email = account.Email, Password = ValidPassword }));

        Assert.That(ex!.Message, Does.Contain("not active"));
    }

    [Test]
    public async Task SignInAsync_EmailNotVerified_Throws()
    {
        var account = BuildActiveAccount();
        account.EmailVerified = false;
        _accountRepo.Setup(r => r.GetByEmailAsync(account.Email)).ReturnsAsync(account);

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SignInAsync(new LoginRequestDto { Email = account.Email, Password = ValidPassword }));

        Assert.That(ex!.Message, Does.Contain("not verified"));
    }

    // ── LogOutAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task LogOutAsync_ValidAccount_ClearsTokenAndCommits()
    {
        var account = BuildActiveAccount();
        account.RefreshToken = "old-token";
        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateAsync(It.IsAny<Account>())).ReturnsAsync(true);

        var result = await _service.LogOutAsync(1);

        Assert.That(result, Is.EqualTo("logged out successfully"));
        Assert.That(account.RefreshToken, Is.Null);
        _unitOfWork.Verify(u => u.BeginTransaction(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Test]
    public async Task LogOutAsync_AccountNotFound_ThrowsAndRollsBack()
    {
        _accountRepo.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync((Account?)null);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.LogOutAsync(99));

        Assert.That(ex!.Message, Does.Contain("account not found"));
        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    [Test]
    public async Task LogOutAsync_RepositoryThrows_RollsBack()
    {
        _accountRepo.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync(BuildActiveAccount());
        _accountRepo.Setup(r => r.UpdateAsync(It.IsAny<Account>())).ThrowsAsync(new Exception("db error"));

        Assert.ThrowsAsync<Exception>(() => _service.LogOutAsync(1));

        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    // ── RefreshTokenAsync ────────────────────────────────────────────────────

    [Test]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
    {
        SetupJwt();
        var account = BuildActiveAccount();
        account.RefreshToken        = "valid-refresh";
        account.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddHours(1);

        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateAsync(It.IsAny<Account>())).ReturnsAsync(true);
        _passwordHelper.Setup(p => p.GenerateRefreshToken()).Returns("new-refresh-token");

        var result = await _service.RefreshTokenAsync(new RefreshTokenRequestDto
        {
            AccountId    = 1,
            RefreshToken = "valid-refresh"
        });

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.RefreshToken, Is.EqualTo("new-refresh-token"));
    }

    [Test]
    public async Task RefreshTokenAsync_AccountNotFound_Throws()
    {
        _accountRepo.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync((Account?)null);

        var ex = Assert.ThrowsAsync<Exception>(() =>
            _service.RefreshTokenAsync(new RefreshTokenRequestDto { AccountId = 1, RefreshToken = "token" }));

        Assert.That(ex!.Message, Does.Contain("invalid refresh token"));
        // Nenhuma transação aberta: não há BeginTransaction nem Rollback
        _unitOfWork.Verify(u => u.BeginTransaction(), Times.Never);
    }

    [Test]
    public async Task RefreshTokenAsync_TokenMismatch_CommitsInvalidationAndThrows()
    {
        var account = BuildActiveAccount();
        account.RefreshToken           = "different-token";
        account.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddHours(1);

        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateAsync(It.IsAny<Account>())).ReturnsAsync(true);

        var ex = Assert.ThrowsAsync<Exception>(() =>
            _service.RefreshTokenAsync(new RefreshTokenRequestDto { AccountId = 1, RefreshToken = "wrong-token" }));

        Assert.That(ex!.Message, Does.Contain("invalid refresh token"));
        // Invalida o token no banco (commit), não rollback
        _unitOfWork.Verify(u => u.BeginTransaction(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
        _unitOfWork.Verify(u => u.Rollback(), Times.Never);
    }

    [Test]
    public async Task RefreshTokenAsync_TokenExpired_CommitsInvalidationAndThrows()
    {
        var account = BuildActiveAccount();
        account.RefreshToken           = "expired-token";
        account.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddHours(-1);

        _accountRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateAsync(It.IsAny<Account>())).ReturnsAsync(true);

        var ex = Assert.ThrowsAsync<Exception>(() =>
            _service.RefreshTokenAsync(new RefreshTokenRequestDto { AccountId = 1, RefreshToken = "expired-token" }));

        Assert.That(ex!.Message, Does.Contain("invalid refresh token"));
        _unitOfWork.Verify(u => u.BeginTransaction(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
        _unitOfWork.Verify(u => u.Rollback(), Times.Never);
    }
}
