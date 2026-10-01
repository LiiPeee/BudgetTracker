using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Dtos.Request.Transaction;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Enum;
using BudgetTracker.Core.Domain.Models.Request.Transaction;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.Service;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Infrastructure.Repository;

namespace BudgetTracker.Application.Service;

public class TransactionsAppService : ITransactionsAppService
{
    private readonly ITransactionsRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IContactRepository _contactRepository;
    private readonly ISubCategoryRepository _subCategoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TransactionsAppService(ITransactionsRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IContactRepository contactRepository,
        ISubCategoryRepository subCategoryRepository,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _contactRepository = contactRepository;
        _accountRepository = accountRepository;
        _subCategoryRepository = subCategoryRepository;
        _unitOfWork = unitOfWork;
    }

    // CREATE TRANSACTION
    public async Task<List<Transactions>> CreateAsync(long accountId, CreateTrasactionRequest transactionRequest)
    {
        try
        {
            if (transactionRequest.Amount <= 0)
                throw new ArgumentException("amount must be greater than zero");

            _unitOfWork.BeginTransaction();

            var contact = await _contactRepository.GetByNameAsync(accountId, transactionRequest.ContactName);
            var category = await _categoryRepository.GetByNameAsync(transactionRequest.CategoryName)
                ?? throw new KeyNotFoundException("we cannot find category for this transaction");

            if (contact is null)
            {
                throw new KeyNotFoundException("we cannot find contact or category for this transaction");
            }

            var subCategory = await _subCategoryRepository.GetByNameAsync(accountId, transactionRequest.SubCategoryName, category.Id)
                ?? await _subCategoryRepository.AddAsync(new SubCategory
                { Name = transactionRequest.SubCategoryName, IsActive = true, CategoryId = category.Id, AccountId = accountId });

            var recurrenceId = (long)transactionRequest.Recurrence;
            var typeTransactionId = (long)transactionRequest.TypeTransaction;

            if (transactionRequest.NumberOfInstallment > 0)
            {
                return await CreateInstallemntsAsync(transactionRequest, category.Id, contact.Id, recurrenceId, typeTransactionId, accountId, subCategory.Id);
            }

            var competenceDate = recurrenceId == (long)Recurrence.OCCASIONALLY
                ? DateTime.UtcNow.AddMonths(1)
                : DateTime.UtcNow;

            Transactions transaction = new()
            {
                AccountId = accountId,
                Amount = transactionRequest.Amount,
                Name = transactionRequest.TransactionName,
                CategoryId = category.Id,
                ContactId = contact.Id,
                SubCategoryId = subCategory.Id,
                Description = transactionRequest.Description,
                NumberOfInstallment = transactionRequest.NumberOfInstallment,
                Paid = false,
                RecurrenceId = recurrenceId,
                TypeTransactionId = typeTransactionId,
                CompetenceDate = competenceDate,
            };

            var savedTransaction = await _transactionRepository.AddAsync(transaction);
            _unitOfWork.Commit();

            return new List<Transactions> { savedTransaction };
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    // EDIT TRANSACTION (full update of an owned transaction)
    public async Task EditTransactionAsync(long accountId, long id, EditTransactionRequest transactionRequest)
    {
        try
        {
            if (transactionRequest.Amount <= 0)
                throw new ArgumentException("amount must be greater than zero");

            var existing = await _transactionRepository.GetByIdAsync(id, accountId)
                ?? throw new UnauthorizedAccessException("Transaction not found or access denied");

            var contact = await _contactRepository.GetByNameAsync(accountId, transactionRequest.ContactName)
                ?? throw new KeyNotFoundException("we cannot find contact for this transaction");

            var category = await _categoryRepository.GetByNameAsync(transactionRequest.CategoryName)
                ?? throw new KeyNotFoundException("we cannot find category for this transaction");

            _unitOfWork.BeginTransaction();

            var subCategory = await _subCategoryRepository.GetByNameAsync(accountId, transactionRequest.SubCategoryName, category.Id)
                ?? await _subCategoryRepository.AddAsync(new SubCategory
                { Name = transactionRequest.SubCategoryName, IsActive = true, CategoryId = category.Id, AccountId = accountId });

            existing.Amount = transactionRequest.Amount;
            existing.CategoryId = category.Id;
            existing.ContactId = contact.Id;
            existing.SubCategoryId = subCategory.Id;
            existing.TypeTransactionId = (long)transactionRequest.TypeTransaction;
            existing.Paid = transactionRequest.Paid;
            existing.NumberOfInstallment = transactionRequest.NumberOfInstallment;
            existing.UpdatedAt = DateTime.UtcNow;

            await _transactionRepository.UpdateAsync(existing);

            _unitOfWork.Commit();
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    // UPDATE TRANSACTION TO PAIDED
    public async Task PaidAsync(long accountId, PaidTransactionRequest paidTransactionRequest)
    {
        try
        {
            var transaction = await _transactionRepository.GetByIdAsync(paidTransactionRequest.TransactionId, accountId);

            if (transaction is null)
                throw new UnauthorizedAccessException("Transaction not found or access denied");

            _unitOfWork.BeginTransaction();

            var flipped = await _transactionRepository.MarkAsPaidAsync(transaction.Id, accountId, paidTransactionRequest.Paid);

            _unitOfWork.Commit();
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    // DELETE TRANSACTION
    public async Task DeleteAsync(long accountId, long id)
    {
        try
        {
            _unitOfWork.BeginTransaction();

            await _transactionRepository.DeleteAsync(id, accountId);

            _unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    // GET TRANSACTION WITH CONTACT
    public async Task<List<FilterByMonthAndYearOutPut>> FilterExpenseWithContactAsync(long accountId, long year, long month)
    {
        try
        {
            var transactions = await _transactionRepository.FilterExpenseMonthWithContactAsync(accountId, year, month);
            var filter = new List<FilterByMonthAndYearOutPut>();

            foreach (var t in transactions)
            {
                var outputFilter = new FilterByMonthAndYearOutPut()
                {
                    Amount = t.Amount,
                    Description = t.Description,
                    Contact = new ContactOutput
                    {
                        Email = t.Contact.Email,
                        Name = t.Contact.Name,
                        Phone = t.Contact.Phone
                    },
                    QuantityOfInstallment = t.QuantityInstallment,
                    Name = t.Name,
                    Paid = t.Paid,
                    CompetenceDate = t.CompetenceDate
                };

                filter.Add(outputFilter);
            }

            return filter;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // GET TRANSACTION EXPENSE BY MONTH AND YEAR
    public async Task<decimal> FilterExpenseMonthAndYearAsync(long accountId, long year, long month)
    {
        try
        {
            var transactions = await _transactionRepository.FilterExpenseMonthAndYearAsync(accountId, year, month);

            decimal totalExpense = 0;

            foreach (var transaction in transactions)
            {
                totalExpense += transaction.Amount;
            }

            return totalExpense;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // GET TRANSACTION INCOME BY MONTH AND YEAR
    public async Task<decimal> FilterIncomeMonthAndYearAsync(long accountId, long year, long month)
    {
        try
        {
            var transactions = await _transactionRepository.FilterIncomeMonthAndYearAsync(accountId, year, month);

            decimal totalIncome = 0;

            foreach (var transaction in transactions)
            {
                totalIncome += transaction.Amount;
            }

            return totalIncome;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // GET TRANSANCTION BY TYPE
    public async Task<IPagedResult<FilterByMonthAndYearOutPut>> FilterTransactionByTypeAsync(long accountId, TypeTransaction type, long month, long year, int pageNumber = 1)
    {
        try
        {
            var transactions = await _transactionRepository.FilterTransactionsByTypeAsync(accountId, type.ToString(), month, year, pageNumber);

            var filter = new List<FilterByMonthAndYearOutPut>();

            foreach (var i in transactions.Items)
            {
                filter.Add(new FilterByMonthAndYearOutPut
                {
                    Id = i.Id,
                    Amount = i.Amount,
                    Description = i.Description,
                    Name = i.Name,
                    Paid = i.Paid,
                    CreatedDate = i.CreatedAt,
                    TypeTransaction = i.TypeTransactionId,
                    Recurrence = i.RecurrenceId,
                    Contact = new ContactOutput
                    {
                        Email = i.Contact.Email,
                        Name = i.Contact.Name,
                        Phone = i.Contact.Phone
                    },
                    Category = new CategoryOutput
                    {
                        Name = i.Category.Name,
                    },
                    QuantityOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.QuantityInstallment : null,
                    DateOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.DateOfInstallment : null,
                    CompetenceDate = i.CompetenceDate
                });
            }

            return new IPagedResult<FilterByMonthAndYearOutPut>
            {
                PageNumber = transactions.PageNumber,
                PageSize = transactions.PageSize,
                TotalRecords = transactions.TotalRecords,
                Items = filter
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // GET TRANSACTION BY CATEGORY, TYPE, MONTH AND YEAR
    public async Task<IPagedResult<FilterByMonthAndYearOutPut>> FilterTransactionsByCategoryAsync(long accountId, Categories categoryName, TypeTransaction type, long month, long year, int pageNumber = 1)
    {
        try
        {
            var transactions = await _transactionRepository.FilterTransactionsByCategoryAsync(accountId, categoryName.ToString(), type.ToString(), month, year, pageNumber);

            var filter = new List<FilterByMonthAndYearOutPut>();

            foreach (var i in transactions.Items)
            {
                filter.Add(new FilterByMonthAndYearOutPut
                {
                    Id = i.Id,
                    Amount = i.Amount,
                    Description = i.Description,
                    Name = i.Name,
                    Paid = i.Paid,
                    TypeTransaction = i.TypeTransactionId,
                    Recurrence = i.RecurrenceId,
                    Contact = new ContactOutput
                    {
                        Email = i.Contact.Email,
                        Name = i.Contact.Name,
                        Phone = i.Contact.Phone
                    },
                    Category = new CategoryOutput
                    {
                        Name = i.Category.Name,
                    },
                    QuantityOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.QuantityInstallment : null,
                    DateOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.DateOfInstallment : null,
                    CompetenceDate = i.CompetenceDate
                });
            }

            return new IPagedResult<FilterByMonthAndYearOutPut>
            {
                PageNumber = transactions.PageNumber,
                PageSize = transactions.PageSize,
                TotalRecords = transactions.TotalRecords,
                Items = filter
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // GET TRANSACTION BY MONTH AND YEAR
    public async Task<IPagedResult<FilterByMonthAndYearOutPut>> FilterByMonthAndYearAsync(long accountId, long month, long year, int pageNumber = 1)
    {
        try
        {
            var transactions = await _transactionRepository.FilterByMonthAndYearAsync(accountId, month, year, pageNumber);

            var filter = new List<FilterByMonthAndYearOutPut>();

            foreach (var i in transactions.Items)
            {
                filter.Add(new FilterByMonthAndYearOutPut
                {
                    Id = i.Id,
                    Amount = i.Amount,
                    Description = i.Description,
                    Name = i.Name,
                    Paid = i.Paid,
                    TypeTransaction = i.TypeTransactionId,
                    Recurrence = i.RecurrenceId,
                    Contact = new ContactOutput
                    {
                        Email = i.Contact.Email,
                        Name = i.Contact.Name,
                        Phone = i.Contact.Phone
                    },
                    Category = new CategoryOutput
                    {
                        Name = i.Category.Name,
                    },
                    QuantityOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.QuantityInstallment : null,
                    DateOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.DateOfInstallment : null,
                    CompetenceDate = i.CompetenceDate
                });
            }

            return new IPagedResult<FilterByMonthAndYearOutPut>
            {
                PageNumber = transactions.PageNumber,
                PageSize = transactions.PageSize,
                TotalRecords = transactions.TotalRecords,
                Items = filter
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<IPagedResult<FilterByMonthAndYearOutPut>> FilterAllInstallmentsAsync(long accountId, long month, long year, TypeTransaction type, int pageNumber = 1)
    {
        try
        {
            var transactions = await _transactionRepository.FilterAllInstallmentsAsync(accountId, month, year, type.ToString(), pageNumber);

            var filter = new List<FilterByMonthAndYearOutPut>();

            foreach (var i in transactions.Items)
            {
                filter.Add(new FilterByMonthAndYearOutPut
                {
                    Id = i.Id,
                    Amount = i.Amount,
                    Description = i.Description,
                    Name = i.Name,
                    Paid = i.Paid,
                    TypeTransaction = i.TypeTransactionId,
                    Recurrence = i.RecurrenceId,
                    Contact = new ContactOutput
                    {
                        Email = i.Contact.Email,
                        Name = i.Contact.Name,
                        Phone = i.Contact.Phone
                    },
                    Category = new CategoryOutput
                    {
                        Name = i.Category.Name,
                    },
                    QuantityOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.QuantityInstallment : null,
                    DateOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.DateOfInstallment : null,
                    CompetenceDate = i.CompetenceDate
                });
            }

            return new IPagedResult<FilterByMonthAndYearOutPut>
            {
                PageNumber = transactions.PageNumber,
                PageSize = transactions.PageSize,
                TotalRecords = transactions.TotalRecords,
                Items = filter
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    //GET TRANSACTION BY CONTACT AND TYPE
    public async Task<IPagedResult<FilterByMonthAndYearOutPut>> FilterByContactAndMonth(long accountId, long year, long month, TypeTransaction type, long contactId, int pageNumber = 1)
    {

        var transactions = await _transactionRepository.FilterByContactAsync(accountId, year, month, type.ToString(), contactId, pageNumber);

        var filter = new List<FilterByMonthAndYearOutPut>();

        foreach (var i in transactions.Items)
        {
            filter.Add(new FilterByMonthAndYearOutPut
            {
                Id = i.Id,
                Amount = i.Amount,
                Description = i.Description,
                Name = i.Name,
                Paid = i.Paid,
                TypeTransaction = i.TypeTransactionId,
                Recurrence = i.RecurrenceId,
                Contact = new ContactOutput
                {
                    Email = i.Contact.Email,
                    Name = i.Contact.Name,
                    Phone = i.Contact.Phone
                },
                Category = new CategoryOutput
                {
                    Name = i.Category.Name,
                },
                QuantityOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.QuantityInstallment : null,
                DateOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.DateOfInstallment : null,
                CompetenceDate = i.CompetenceDate
            });
        }

        return new IPagedResult<FilterByMonthAndYearOutPut>
        {
            PageNumber = transactions.PageNumber,
            PageSize = transactions.PageSize,
            TotalRecords = transactions.TotalRecords,
            Items = filter
        };
    }

    // GET ECONOMY
    public async Task<decimal> GetEconomyAsync(long accountId, long year, long month)
    {
        try
        {
            var expense = await _transactionRepository.FilterExpenseMonthAndYearAsync(accountId, year, month);
            var income = await _transactionRepository.FilterIncomeMonthAndYearAsync(accountId, year, month);

            var totalExpense = 0m;
            var totalIncome = 0m;

            foreach (var e in expense)
            {
                totalExpense += e.Amount;
            }

            foreach (var i in income)
            {
                totalIncome += i.Amount;
            }

            var total = totalIncome - totalExpense;

            return total;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // CREATE TRANSACTION INSTALLEMNTS
    private async Task<List<Transactions>> CreateInstallemntsAsync(CreateTrasactionRequest request, long category, long contactId, long recurrenceId, long typeTransactionId, long accountId, long? subCategoryId)
    {
        try
        {
            var dateNow = DateTime.UtcNow;
            List<Transactions> transactions = new List<Transactions>();

            for (var i = 1; i <= request.NumberOfInstallment; i++)
            {
                var dateInstallemnts = new DateTime(dateNow.Year, dateNow.Month, (int)request.DateOfInstallment).AddMonths(i);

                Transactions transaction = new()
                {
                    AccountId = accountId,
                    Amount = request.Amount,
                    Name = request.TransactionName,
                    CategoryId = category,
                    ContactId = contactId,
                    SubCategoryId = subCategoryId,
                    QuantityInstallment = $"{i}/{request.NumberOfInstallment}",
                    DateOfInstallment = dateInstallemnts,
                    CompetenceDate = dateInstallemnts,
                    Description = request.Description,
                    NumberOfInstallment = request.NumberOfInstallment,
                    Paid = false,
                    RecurrenceId = recurrenceId,
                    TypeTransactionId = typeTransactionId,
                };

                var savedTransaction = await _transactionRepository.AddAsync(transaction);
                transactions.Add(savedTransaction);
            }

            _unitOfWork.Commit();

            return transactions;
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    public async Task<IPagedResult<FilterByMonthAndYearOutPut>> FilterTransactionByPaidAsync(long accountId, long month, long year, bool paid, int pageNumber = 1)
    {

        var transactions = await _transactionRepository.FilterByPaidAsync(accountId, month, year, paid, pageNumber);

        var filter = new List<FilterByMonthAndYearOutPut>();

        foreach (var i in transactions.Items)
        {
            filter.Add(new FilterByMonthAndYearOutPut
            {
                Id = i.Id,
                Amount = i.Amount,
                Description = i.Description,
                Name = i.Name,
                Paid = i.Paid,
                TypeTransaction = i.TypeTransactionId,
                Recurrence = i.RecurrenceId,
                Contact = new ContactOutput
                {
                    Email = i.Contact.Email,
                    Name = i.Contact.Name,
                    Phone = i.Contact.Phone
                },
                Category = new CategoryOutput
                {
                    Name = i.Category.Name,
                },
                QuantityOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.QuantityInstallment : null,
                DateOfInstallment = !string.IsNullOrEmpty(i.QuantityInstallment) ? i.DateOfInstallment : null,
                CompetenceDate = i.CompetenceDate
            });
        }

        return new IPagedResult<FilterByMonthAndYearOutPut>
        {
            PageNumber = transactions.PageNumber,
            PageSize = transactions.PageSize,
            TotalRecords = transactions.TotalRecords,
            Items = filter
        };
    }
}


