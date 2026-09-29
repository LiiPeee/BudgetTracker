
using System.Security.Claims;
using BudgetTracker.Core.Domain.Models.Output;
using BudgetTracker.Core.Domain.Models.Request.Account;
using BudgetTracker.Core.Domain.Service;
using BudgetTracker.WebApi.Models.Auth;
using BudgetTracker.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.WebApi.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(IAuthenticationAppService accountAppService, IExpiredTokenValidator expiredTokenValidator) : ControllerBase
    {
        private readonly IAuthenticationAppService _accountAppService = accountAppService;
        private readonly IExpiredTokenValidator _expiredTokenValidator = expiredTokenValidator;

        [HttpPost("[action]")]
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
        public async Task<ActionResult<string>> VerifyTokenAsync([FromQuery] VerifyTokenRequest request)
        {
            var requestDto = new VerifyTokenRequestDto()
            {
                Id = request.Id,
                Token = request.Token
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
        public async Task<ActionResult<string>> EmailVerifycationAsync([FromBody] string email)
        {
            return Ok(await _accountAppService.VerifyEmailAsync(email));
        }
        [HttpPost("[action]")]
        public async Task<ActionResult<string>> ValidateResetCode([FromQuery] string email, [FromBody] string token)
        {
            return Ok(await _accountAppService.ValidateResetCodeAsync(email, token));
        }
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

