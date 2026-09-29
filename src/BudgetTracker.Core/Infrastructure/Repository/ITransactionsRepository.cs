using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Entities;
using System;

namespace BudgetTracker.Core.Domain.Repository;

public interface ITransactionsRepository : IAccountScopedRepository<Transactions>
{
    Task<IPagedResult<Transactions>> FilterTransactionsByCategoryAsync(long accountId, string categoryName, string type, long month, long year, int pageNumber = 1);
    Task<IPagedResult<Transactions>> FilterByMonthAndYearAsync(long accountId, long month, long year, int pageNumber = 1);
    Task<IPagedResult<Transactions>> FilterByContactAsync(long accountId, long year, long month, string type, long contactId, int pageNumber = 1);
    Task<decimal> FilterExpenseMonthAndYearAsync(long accountId, long year, long month);
    Task<IPagedResult<Transactions>> FilterAllInstallmentsAsync(long accountId, long month, long year, string type, int pageNumber = 1);
    Task<decimal> FilterIncomeMonthAndYearAsync(long accountId, long year, long month);
    Task<List<Transactions>> FilterExpenseMonthWithContactAsync(long accountId, long year, long month);
    Task<IPagedResult<Transactions>> FilterTransactionsByTypeAsync(long accountId, string type, long month, long year, int pageNumber = 1);
    Task<bool> MarkAsPaidAsync(long id, long accountId, bool paid);
    Task<decimal> GetExpenseTotalByCategoryAsync(long accountId, long categoryId, int month, int year);
    Task<IPagedResult<Transactions>> FilterByPaidAsync(long accountId, long month, long year, bool paid, int pageNumber = 1);
}


