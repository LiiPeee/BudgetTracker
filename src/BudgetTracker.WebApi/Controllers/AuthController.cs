
using System.Security.Claims;
using System.Text.Json;
using BudgetTracker.Core.Domain.Models.Output;
using BudgetTracker.Core.Domain.Models.Request.Account;
using BudgetTracker.Core.Domain.Service;
using BudgetTracker.WebApi.Filters;
using BudgetTracker.WebApi.Models.Auth;
using BudgetTracker.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BudgetTracker.WebApi.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(IAuthenticationAppService accountAppService, IExpiredTokenValidator expiredTokenValidator) : ControllerBase
    {
        private readonly IAuthenticationAppService _accountAppService = accountAppService;
        private readonly IExpiredTokenValidator _expiredTokenValidator = expiredTokenValidator;

        private const int EmailResponseMilliseconds = 2000;

        [HttpPost("[action]")]
        [MinimumDuration(EmailResponseMilliseconds)]
        public async Task<ActionResult<CreateAccountDto>> SignUpAsync([FromBody] CreateAccountRequestDto request)
        {
            return Ok(await _accountAppService.SignUpAsync(request));
        }

        [HttpPost("[action]")]
        public async Task<ActionResult<TokenResponseDto?>> SignInAsync([FromBody] LoginRequestDto request)
        {
            var login = await _accountAppService.SignInAsync(request);
            if (login == null)
            {
                return Unauthorized();
            }

            return Ok(login);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult<TokenResponseDto?>> SignInGoogleAsync([FromBody] GoogleLoginRequestDto request)
        {
            var login = await _accountAppService.SignInGoogleAsync(request);
            if (login == null)
            {
                return Unauthorized();
            }

            return Ok(login);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult<string>> VerifyTokenAsync(
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VerifyTokenRequest? request,
            // Compatibilidade com o front antigo; remover depois que o novo estiver publicado.
            [FromQuery] string? id,
            [FromQuery] string? token)
        {
            var requestDto = new VerifyTokenRequestDto()
            {
                Id = request?.Id ?? id ?? throw new ArgumentException("Invalid Token"),
                Token = request?.Token ?? token ?? throw new ArgumentException("Invalid Token")
            };

            return Ok(await _accountAppService.VerifyTokenSignUpAsync(requestDto));
        }

        [HttpPost("[action]")]
        public async Task<ActionResult<string>> ResetPasswordAsync([FromBody] ResetPasswordRequest request)
        {
            var resetPassword = new ResetPasswordRequestDto()
            {
                email = request.email,
                newPassword = request.newPassword,
                token = request.token
            };

            return Ok(await _accountAppService.ResetPasswordAsync(resetPassword));
        }

        [HttpPost("[action]")]
        [MinimumDuration(EmailResponseMilliseconds)]
        public async Task<ActionResult<string>> EmailVerifycationAsync([FromBody] string email)
        {
            return Ok(await _accountAppService.VerifyEmailAsync(email));
        }
        [HttpPost("[action]")]
        public async Task<ActionResult<string>> ValidateResetCode([FromBody] JsonElement body, [FromQuery] string? email)
        {
            // Formato atual: { email, token }. O antigo (token como texto, e-mail na URL) continua
            // aceito até o front novo estar publicado.
            var isObject = body.ValueKind == JsonValueKind.Object;
            var address = isObject ? ReadString(body, "email") : email;
            var code = isObject ? ReadString(body, "token") : body.ValueKind == JsonValueKind.String ? body.GetString() : null;

            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("email and token are required");

            return Ok(await _accountAppService.ValidateResetCodeAsync(address, code));
        }

        private static string? ReadString(JsonElement body, string name)
            => body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        [HttpPost("[action]")]
        public async Task<ActionResult<TokenResponseDto?>> RefreshTokenAsync(RefreshTokenAccountRequest request)
        {
            var accountId = _expiredTokenValidator.GetAccountIdFromExpiredToken(Request.Headers.Authorization.ToString());

            if (accountId is null)
                return Unauthorized();

            var requestMapper = new RefreshTokenRequestDto()
            {
                AccountId = accountId.Value,
                RefreshToken = request.RefreshToken
            };

            var result = await _accountAppService.RefreshTokenAsync(requestMapper);

            return Ok(result);
        }

        [HttpPost("[action]")]
        [Authorize(Roles = "User,Admin")]
        public async Task<ActionResult<TokenResponseDto?>> LogOutAsync()
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await _accountAppService.LogOutAsync(accountId);

            if (result is null)
            {
                return BadRequest();
            }
            return Ok(new { message = result });
        }
    }
}

