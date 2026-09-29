using System.Security.Claims;
using BudgetTracker.Application.Dtos.Request;
using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Models.Request.Contact;
using BudgetTracker.Core.Domain.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.WebApi.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "User,Admin")]
    public class ContactController : ControllerBase
    {
        private readonly IContactAppService _contactAppService;
        public ContactController(IContactAppService contactappService)
        {
            _contactAppService = contactappService;
        }

        [HttpPost("[action]")]
        public async Task<ContactOutput?> CreateAsync([FromBody] CreateContactRequest contactRequest)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            return await _contactAppService.CreateAsync(accountId, contactRequest);
        }

        [HttpGet("[action]")]
        public async Task<List<ContactOutput?>> GetAllAsync()
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            return await _contactAppService.GetAllsync(accountId);
        }
        [HttpPost("[action]")]
        public async Task EditContactAsync([FromBody] ContactRequest contactRequest)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            await _contactAppService.EditContactAsync(accountId, contactRequest);
        }

        [HttpDelete("[action]")]
        public async Task DeleteContactAsync([FromQuery] string contactId)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            await _contactAppService.DeleteContactAsync(accountId, contactId);
        }
    }
}

