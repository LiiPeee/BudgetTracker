using BudgetTracker.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Test;

public class ExpiredTokenValidatorTest
{
    private const string TestKey = "0123456789012345678901234567890123456789012345678901234567890123";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";
    private const long AccountId = 42;

    private static TokenValidationParameters BuildParameters() => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
        ClockSkew = TimeSpan.Zero
    };

    private static IExpiredTokenValidator CreateValidator(TokenValidationParameters? parameters = null)
    {
        var options = new JwtBearerOptions { TokenValidationParameters = parameters ?? BuildParameters() };
        var optionsMock = new Mock<IOptionsMonitor<JwtBearerOptions>>();
        optionsMock.Setup(o => o.CurrentValue).Returns(options);
        return new ExpiredTokenValidator(optionsMock.Object);
    }

    private static string GenerateToken(DateTime expiresAt)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, AccountId.ToString()),
            new Claim(ClaimTypes.Name, "user@test.com"),
            new Claim(ClaimTypes.Role, "User")
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
            SecurityAlgorithms.HmacSha512);

        var descriptor = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(descriptor);
    }

    [Test]
    public void GetAccountId_ExpiredToken_ReturnsAccountId()
    {
        var validator = CreateValidator();
        var expiredToken = GenerateToken(DateTime.UtcNow.AddMinutes(-10));

        var result = validator.GetAccountIdFromExpiredToken($"Bearer {expiredToken}");

        Assert.That(result, Is.EqualTo(AccountId));
    }

    [Test]
    public void GetAccountId_ValidToken_ReturnsAccountId()
    {
        var validator = CreateValidator();
        var validToken = GenerateToken(DateTime.UtcNow.AddMinutes(30));

        var result = validator.GetAccountIdFromExpiredToken($"Bearer {validToken}");

        Assert.That(result, Is.EqualTo(AccountId));
    }

    [Test]
    public void GetAccountId_NullHeader_ReturnsNull()
    {
        var validator = CreateValidator();

        Assert.That(validator.GetAccountIdFromExpiredToken(null), Is.Null);
    }

    [Test]
    public void GetAccountId_EmptyHeader_ReturnsNull()
    {
        var validator = CreateValidator();

        Assert.That(validator.GetAccountIdFromExpiredToken(""), Is.Null);
    }

    [Test]
    public void GetAccountId_NoBearerPrefix_ReturnsNull()
    {
        var validator = CreateValidator();
        var token = GenerateToken(DateTime.UtcNow.AddMinutes(-5));

        Assert.That(validator.GetAccountIdFromExpiredToken(token), Is.Null);
    }

    [Test]
    public void GetAccountId_TamperedSignature_ReturnsNull()
    {
        var validator = CreateValidator();
        var token = GenerateToken(DateTime.UtcNow.AddMinutes(-5));
        var tampered = token[..^5] + "AAAAA";

        Assert.That(validator.GetAccountIdFromExpiredToken($"Bearer {tampered}"), Is.Null);
    }

    [Test]
    public void GetAccountId_WrongIssuer_ReturnsNull()
    {
        var validator = CreateValidator();
        var wrongIssuerParams = BuildParameters();
        wrongIssuerParams.ValidIssuer = "wrong-issuer";

        var token = GenerateToken(DateTime.UtcNow.AddMinutes(-5));
        var tokenValue = token;
        var wrongValidator = CreateValidator(wrongIssuerParams);

        Assert.That(wrongValidator.GetAccountIdFromExpiredToken($"Bearer {tokenValue}"), Is.Null);
    }

    [Test]
    public void GetAccountId_GarbageString_ReturnsNull()
    {
        var validator = CreateValidator();

        Assert.That(validator.GetAccountIdFromExpiredToken("Bearer not-a-jwt"), Is.Null);
    }

    [Test]
    public void GetAccountId_CaseInsensitiveBearerPrefix_ReturnsAccountId()
    {
        var validator = CreateValidator();
        var expiredToken = GenerateToken(DateTime.UtcNow.AddMinutes(-10));

        var result = validator.GetAccountIdFromExpiredToken($"bearer {expiredToken}");

        Assert.That(result, Is.EqualTo(AccountId));
    }
}
