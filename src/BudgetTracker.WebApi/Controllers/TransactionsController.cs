using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Dtos.Request.Transaction;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Enum;
using BudgetTracker.Core.Domain.Models.Request.Transaction;
using BudgetTracker.Core.Domain.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace BudgetTracker.WebApi.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "User,Admin")]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionsAppService _transactionAppService;

        public TransactionController(ITransactionsAppService transactionAppService)
        {
            _transactionAppService = transactionAppService;
        }

        [HttpPost("[action]")]
        public async Task<ActionResult<List<Transactions>>> CreateAsync([FromBody] CreateTrasactionRequest transactionRequest)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            return Ok(await _transactionAppService.CreateAsync(accountId, transactionRequest));
        }

        [HttpPatch("[action]")]
        public async Task<ActionResult> PaidAsync([FromBody] PaidTransactionRequest paidTransactionRequest)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _transactionAppService.PaidAsync(accountId, paidTransactionRequest);
            return Ok();
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> EditTransactionAsync([FromQuery] long id, [FromBody] EditTransactionRequest transactionRequest)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _transactionAppService.EditTransactionAsync(accountId, id, transactionRequest);
            return Ok();
        }

        [HttpGet("[action]")]
        public async Task<ActionResult<IPagedResult<FilterByMonthAndYearOutPut>>> GetByCategoryAsync(
            [FromQuery][Required] string categoryName,
            [FromQuery][Range(1, 12)] long month,
            [FromQuery][Range(2000, 2100)] long year,
            [FromQuery] TypeTransaction type, [FromQuery][Range(1, int.MaxValue)] int pageNumber = 1)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(await _transactionAppService.FilterTransactionsByCategoryAsync(accountId, categoryName, type, month, year, pageNumber));
        }

        [HttpGet("[action]")]
        public async Task<ActionResult<IPagedResult<FilterByMonthAndYearOutPut>>> GetByTypeAsync(
            [FromQuery][Range(1, 12)] long month,
            [FromQuery][Range(2000, 2100)] long year,
            [FromQuery] TypeTransaction type,
            [FromQuery][Range(1, int.MaxValue)] int pageNumber = 1)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(await _transactionAppService.FilterTransactionByTypeAsync(accountId, type, month, year, pageNumber));
        }

        [HttpGet("[action]")]
        public async Task<ActionResult<IPagedResult<FilterByMonthAndYearOutPut>>> GetByMonthAndYearAsync(
            [FromQuery][Range(1, 12)] long month,
            [FromQuery][Range(2000, 2100)] long year,
            [FromQuery][Range(1, int.MaxValue)] int pageNumber = 1)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(await _transactionAppService.FilterByMonthAndYearAsync(accountId, month, year, pageNumber));
        }


        [HttpGet("[action]")]
        public async Task<ActionResult<IPagedResult<FilterByMonthAndYearOutPut>>> GetAllInstallmentsAsync(
            [FromQuery][Range(1, 12)] long month,
            [FromQuery][Range(2000, 2100)] long year,
            [FromQuery] TypeTransaction type,
            [FromQuery][Range(1, int.MaxValue)] int pageNumber = 1)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(await _transactionAppService.FilterAllInstallmentsAsync(accountId, month, year, type, pageNumber));
        }


        [HttpGet("[action]")]
        public async Task<ActionResult<IPagedResult<FilterByMonthAndYearOutPut>>> GetByPaidAndMonthAndYearAsync(
           [FromQuery][Range(1, 12)] long month,
           [FromQuery][Range(2000, 2100)] long year,
           [FromQuery] bool paid,
           [FromQuery][Range(1, int.MaxValue)] int pageNumber = 1)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(await _transactionAppService.FilterTransactionByPaidAsync(accountId, month, year, paid, pageNumber));
        }

        [HttpGet("[action]")]
        public async Task<ActionResult<List<FilterByMonthAndYearOutPut>>> GetByContactAsync(
            [FromQuery][Range(2000, 2100)] long year,
            [FromQuery][Range(1, 12)] long month,
            [FromQuery] long contactId,
            [FromQuery] TypeTransaction type, [FromQuery][Range(1, int.MaxValue)] int pageNumber = 1)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(await _transactionAppService.FilterByContactAndMonth(accountId, year, month, type, contactId, pageNumber));
        }

        [HttpGet("[action]")]
        public async Task<ActionResult<decimal>> GetExpenseByMonthAndYearAsync(
            [FromQuery][Range(2000, 2100)] long year,
            [FromQuery][Range(1, 12)] long month)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(await _transactionAppService.FilterExpenseMonthAndYearAsync(accountId, year, month));
        }

        [HttpGet("[action]")]
        public async Task<ActionResult<decimal>> GetIncomeByMonthAndYearAsync(
            [FromQuery][Range(2000, 2100)] long year,
            [FromQuery][Range(1, 12)] long month)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(await _transactionAppService.FilterIncomeMonthAndYearAsync(accountId, year, month));
        }

        [HttpGet("[action]")]
        public async Task<ActionResult<decimal>> GetEconomyAsync(
            [FromQuery][Range(2000, 2100)] long year,
            [FromQuery][Range(1, 12)] long month)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(await _transactionAppService.GetEconomyAsync(accountId, year, month));
        }

        [HttpDelete("[action]")]
        public async Task<ActionResult> DeleteTransactionAsync([FromQuery] long id)
        {
            var accountId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _transactionAppService.DeleteAsync(accountId, id);
            return Ok();
        }
    }
}

