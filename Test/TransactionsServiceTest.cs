using BudgetTracker.Application.Service;
using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Dtos.Request.Transaction;
using BudgetTracker.Core.Domain.Models.Request.Transaction;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Enum;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.Service;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Infrastructure.Repository;
using Moq;

namespace Test;

public class TransactionsServiceTest
{
    private Mock<ITransactionsRepository> _transactionsRepo;
    private Mock<ICategoryRepository> _categoryRepo;
    private Mock<IContactRepository> _contactRepo;
    private Mock<ISubCategoryRepository> _subCategoryRepo;
    private Mock<IAccountRepository> _accountRepo;
    private Mock<IUnitOfWork> _unitOfWork;
    private TransactionsAppService _service;

    [SetUp]
    public void Setup()
    {
        _transactionsRepo   = new Mock<ITransactionsRepository>();
        _categoryRepo       = new Mock<ICategoryRepository>();
        _contactRepo        = new Mock<IContactRepository>();
        _subCategoryRepo    = new Mock<ISubCategoryRepository>();
        _accountRepo        = new Mock<IAccountRepository>();
        _unitOfWork         = new Mock<IUnitOfWork>();

        _service = new TransactionsAppService(
            _transactionsRepo.Object,
            _categoryRepo.Object,
            _contactRepo.Object,
            _subCategoryRepo.Object,
            _accountRepo.Object,
            _unitOfWork.Object);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task CreateAsync_SimpleTransaction_ReturnsSavedTransaction()
    {
        var category    = new Category { Id = 1, Name = "Food" };
        var contact     = new Contact  { Id = 2, Name = "John" };
        var subCategory = new SubCategory { Id = 3, Name = "Lunch" };
        var saved       = new Transactions { Id = 10, Amount = 50, Name = "Lunch out", AccountId = 1 };

        var request = new CreateTrasactionRequest
        {
            Amount          = 50,
            TransactionName = "Lunch out",
            CategoryName    = "Alimentação",
            ContactName     = "John",
            SubCategoryName = "Lunch",
            Description     = "Test",
            Recurrence      = Recurrence.NONE,
            TypeTransaction = TypeTransactions.EXPENSE,
        };

        _categoryRepo.Setup(r => r.GetByNameAsync(It.IsAny<string>())).ReturnsAsync(category);
        _contactRepo.Setup(r => r.GetByNameAsync(1, "John")).ReturnsAsync(contact);
        _subCategoryRepo.Setup(r => r.GetByNameAsync(1, "Lunch", It.IsAny<long?>())).ReturnsAsync(subCategory);
        _transactionsRepo.Setup(r => r.AddAsync(It.IsAny<Transactions>())).ReturnsAsync(saved);

        var result = await _service.CreateAsync(1, request);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(10));
        _unitOfWork.Verify(u => u.BeginTransaction(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Test]
    public async Task CreateAsync_CategoryNotFound_ThrowsKeyNotFoundException()
    {
        _categoryRepo.Setup(r => r.GetByNameAsync(It.IsAny<string>())).ReturnsAsync((Category?)null);
        _contactRepo.Setup(r => r.GetByNameAsync(It.IsAny<long>(), It.IsAny<string>())).ReturnsAsync(new Contact { Id = 1, Name = "X", TypeContactId = 1, AccountId = 1 });

        var request = new CreateTrasactionRequest
        {
            Amount          = 10,
            TransactionName = "Test",
            CategoryName    = "CategoriaInexistente",
            ContactName     = "X",
            SubCategoryName = "Sub",
            Recurrence      = Recurrence.NONE,
            TypeTransaction = TypeTransactions.EXPENSE,
        };

        Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CreateAsync(1, request));
        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    // ── PaidAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task PaidAsync_MarksAsPaid_WithoutTouchingBalance()
    {
        // Since #18 the balance is derived from paid transactions, so PaidAsync
        // must only flip the flag — never call UpdateBalanceAtomicAsync.
        var transaction = new Transactions { Id = 5, AccountId = 1, Amount = 30, Paid = false, TypeTransactionId = 2, Name = "Test" };

        _transactionsRepo.Setup(r => r.GetByIdAsync(5, 1)).ReturnsAsync(transaction);
        _transactionsRepo.Setup(r => r.MarkAsPaidAsync(5, 1, true)).ReturnsAsync(true);

        await _service.PaidAsync(1, new PaidTransactionRequest { TransactionId = 5, Paid = true });

        _transactionsRepo.Verify(r => r.MarkAsPaidAsync(5, 1, true), Times.Once);
        _accountRepo.Verify(r => r.UpdateBalanceAtomicAsync(It.IsAny<long>(), It.IsAny<decimal>()), Times.Never);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Test]
    public async Task PaidAsync_UnpaidFlag_DoesNotTouchBalance()
    {
        // Flipping back to unpaid must also leave the balance untouched.
        var transaction = new Transactions { Id = 5, AccountId = 1, Amount = 30, Paid = true, TypeTransactionId = 1, Name = "Test" };

        _transactionsRepo.Setup(r => r.GetByIdAsync(5, 1)).ReturnsAsync(transaction);
        _transactionsRepo.Setup(r => r.MarkAsPaidAsync(5, 1, false)).ReturnsAsync(true);

        await _service.PaidAsync(1, new PaidTransactionRequest { TransactionId = 5, Paid = false });

        _transactionsRepo.Verify(r => r.MarkAsPaidAsync(5, 1, false), Times.Once);
        _accountRepo.Verify(r => r.UpdateBalanceAtomicAsync(It.IsAny<long>(), It.IsAny<decimal>()), Times.Never);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Test]
    public async Task PaidAsync_LostTheRace_DoesNotTouchBalance()
    {
        // MarkAsPaidAsync returns false → another request already flipped it; no double-debit.
        var transaction = new Transactions { Id = 5, AccountId = 1, Amount = 30, Paid = false, TypeTransactionId = 1, Name = "Test" };

        _transactionsRepo.Setup(r => r.GetByIdAsync(5, 1)).ReturnsAsync(transaction);
        _transactionsRepo.Setup(r => r.MarkAsPaidAsync(5, 1, true)).ReturnsAsync(false);

        await _service.PaidAsync(1, new PaidTransactionRequest { TransactionId = 5, Paid = true });

        _accountRepo.Verify(r => r.UpdateBalanceAtomicAsync(It.IsAny<long>(), It.IsAny<decimal>()), Times.Never);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Test]
    public async Task PaidAsync_TransactionBelongsToDifferentAccount_ThrowsUnauthorizedAccessException()
    {
        // The scoped repository returns null when the transaction is not owned by the account.
        _transactionsRepo.Setup(r => r.GetByIdAsync(5, 1)).ReturnsAsync((Transactions?)null);

        var paidRequest = new PaidTransactionRequest { TransactionId = 5, Paid = true };

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.PaidAsync(1, paidRequest));
        _transactionsRepo.Verify(r => r.UpdateAsync(It.IsAny<Transactions>()), Times.Never);
        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    // ── EditTransactionAsync ─────────────────────────────────────────────────

    [Test]
    public async Task EditTransactionAsync_OwnedTransaction_UpdatesFieldsAndCommits()
    {
        var existing = new Transactions { Id = 5, AccountId = 1, Amount = 10, Name = "Old", TypeTransactionId = 1 };
        _transactionsRepo.Setup(r => r.GetByIdAsync(5, 1)).ReturnsAsync(existing);
        _contactRepo.Setup(r => r.GetByNameAsync(1, "John")).ReturnsAsync(new Contact { Id = 2, Name = "John", AccountId = 1 });
        _subCategoryRepo.Setup(r => r.GetByNameAsync(1, "Lunch", It.IsAny<long?>())).ReturnsAsync(new SubCategory { Id = 3, Name = "Lunch" });
        _transactionsRepo.Setup(r => r.UpdateAsync(It.IsAny<Transactions>())).ReturnsAsync(true);

        var request = new EditTransactionRequest
        {
            Amount          = 99,
            TransactionName = "New name",
            CategoryName    = "Alimentação",
            ContactName     = "John",
            SubCategoryName = "Lunch",
            TypeTransaction = TypeTransactions.EXPENSE,
            Paid            = true,
        };

        await _service.EditTransactionAsync(1, 5, request);

        _transactionsRepo.Verify(r => r.UpdateAsync(It.Is<Transactions>(t =>
            t.Id == 5 && t.Amount == 99 && t.Name == "New name" && t.Paid)), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Test]
    public void EditTransactionAsync_NotOwned_ThrowsUnauthorizedAndDoesNotUpdate()
    {
        _transactionsRepo.Setup(r => r.GetByIdAsync(5, 1)).ReturnsAsync((Transactions?)null);

        var request = new EditTransactionRequest
        {
            Amount = 10, TransactionName = "x", CategoryName = "Alimentação", ContactName = "John",
            SubCategoryName = "Lunch", TypeTransaction = TypeTransactions.EXPENSE, Paid = false,
        };

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.EditTransactionAsync(1, 5, request));
        _transactionsRepo.Verify(r => r.UpdateAsync(It.IsAny<Transactions>()), Times.Never);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteAsync_CallsRepository_AndCommits()
    {
        _transactionsRepo.Setup(r => r.DeleteAsync(7, 1)).ReturnsAsync(true);

        await _service.DeleteAsync(1, 7);

        _transactionsRepo.Verify(r => r.DeleteAsync(7, 1), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    // ── FilterExpenseMonthAndYearAsync ───────────────────────────────────────

    [Test]
    public async Task FilterExpenseMonthAndYearAsync_ReturnsSumOfAmounts()
    {
        _transactionsRepo.Setup(r => r.FilterExpenseMonthAndYearAsync(1, 2026, 5)).ReturnsAsync(150m);

        var total = await _service.FilterExpenseMonthAndYearAsync(1, 2026, 5);

        Assert.That(total, Is.EqualTo(150));
    }

    [Test]
    public async Task FilterExpenseMonthAndYearAsync_NoTransactions_ReturnsZero()
    {
        _transactionsRepo.Setup(r => r.FilterExpenseMonthAndYearAsync(1, 2026, 5)).ReturnsAsync(0m);

        var total = await _service.FilterExpenseMonthAndYearAsync(1, 2026, 5);

        Assert.That(total, Is.EqualTo(0));
    }

    // ── FilterExpenseWithContactAsync ────────────────────────────────────────

    [Test]
    public async Task FilterExpenseWithContactAsync_MapsContactCorrectly()
    {
        var transactions = new List<Transactions>
        {
            new()
            {
                Amount = 75, Name = "Dinner", Description = "desc", Paid = false,
                Contact = new Contact { Name = "Alice", Email = "alice@x.com", Phone = "999", TypeContactId = 1, AccountId = 1 }
            }
        };

        _transactionsRepo.Setup(r => r.FilterExpenseMonthWithContactAsync(1, 2026, 5)).ReturnsAsync(transactions);

        var result = await _service.FilterExpenseWithContactAsync(1, 2026, 5);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Contact.Name, Is.EqualTo("Alice"));
        Assert.That(result[0].Amount, Is.EqualTo(75));
    }
}
