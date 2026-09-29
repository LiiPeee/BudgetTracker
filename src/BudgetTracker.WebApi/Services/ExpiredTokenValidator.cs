using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BudgetTracker.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BudgetTracker.WebApi.Services;

public class ExpiredTokenValidator : IExpiredTokenValidator
{
    private const string BearerPrefix = "Bearer ";
    private readonly TokenValidationParameters _baseParameters;

    public ExpiredTokenValidator(IOptionsMonitor<JwtBearerOptions> options)
    {
        _baseParameters = options.CurrentValue.TokenValidationParameters;
    }

    public long? GetAccountIdFromExpiredToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return null;

        if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authorizationHeader[BearerPrefix.Length..].Trim();
        var handler = new JwtSecurityTokenHandler();

        if (!handler.CanReadToken(token))
            return null;

        var parameters = _baseParameters.Clone();
        parameters.ValidateLifetime = false;

        try
        {
            var principal = handler.ValidateToken(token, parameters, out _);
            var claimValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(claimValue, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}
