using BudgetTracker.Application.Service;
using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Infrastructure.Repository;
using Moq;

namespace Test.Application;

/// <summary>
/// Cobertura do BudgetLimitService.GetByAccountIdAsync: percentual de uso,
/// flag de estouro, limite zero, sem gasto e normalização de limite negativo.
/// </summary>
[TestFixture]
public class BudgetLimitServiceTests
{
    private const long AccountId = 1;

    private Mock<IBudgetLimitRepository>  _budgetRepo;
    private Mock<ICategoryRepository>     _categoryRepo;
    private Mock<ITransactionsRepository> _transactionsRepo;
    private Mock<IUnitOfWork>             _unitOfWork;
    private BudgetLimitService            _service;

    [SetUp]
    public void Setup()
    {
        _budgetRepo       = new Mock<IBudgetLimitRepository>();
        _categoryRepo     = new Mock<ICategoryRepository>();
        _transactionsRepo = new Mock<ITransactionsRepository>();
        _unitOfWork       = new Mock<IUnitOfWork>();
        _service          = new BudgetLimitService(_budgetRepo.Object, _unitOfWork.Object, _categoryRepo.Object, _transactionsRepo.Object);
    }

    private void SetupSingleBudget(BudgetLimit budget) =>
        _budgetRepo.Setup(r => r.GetByAccountIdAsync(AccountId, It.IsAny<long>(), AccountId, It.IsAny<int>()))
            .ReturnsAsync(new IPagedResult<BudgetLimit?>
            {
                Items = new List<BudgetLimit?> { budget },
                PageNumber = 1,
                PageSize = 10,
                TotalRecords = 1,
            });

    [Test]
    public async Task GetByAccountIdAsync_UnderLimit_ComputesPercentageAndLeavesLimitUntouched()
    {
        var budget = new BudgetLimit { Id = 1, AccountId = AccountId, CategoryId = 2, Month = 6, Year = 2026, LimitAmount = 200 };
        SetupSingleBudget(budget);
        _transactionsRepo.Setup(r => r.GetExpenseTotalByCategoryAsync(AccountId, 2, 6, 2026)).ReturnsAsync(50m);

        var item = (await _service.GetByAccountIdAsync(AccountId, 1, AccountId)).Items.First();

        Assert.That(item!.Percentage, Is.EqualTo(25m));         // 50 / 200 * 100
        Assert.That(item.IsLimit, Is.False);
        Assert.That(item.LimitAmount, Is.EqualTo(200m), "the configured limit must never be mutated");
    }

    [Test]
    public async Task GetByAccountIdAsync_OverLimit_FlagsIsLimit()
    {
        var budget = new BudgetLimit { Id = 1, AccountId = AccountId, CategoryId = 2, Month = 6, Year = 2026, LimitAmount = 100 };
        SetupSingleBudget(budget);
        _transactionsRepo.Setup(r => r.GetExpenseTotalByCategoryAsync(AccountId, 2, 6, 2026)).ReturnsAsync(150m);

        var item = (await _service.GetByAccountIdAsync(AccountId, 1, AccountId)).Items.First();

        Assert.That(item!.IsLimit, Is.True);
        Assert.That(item.Percentage, Is.EqualTo(150m));
    }

    [Test]
    public async Task GetByAccountIdAsync_ZeroLimit_DoesNotDivideByZero()
    {
        var budget = new BudgetLimit { Id = 1, AccountId = AccountId, CategoryId = 2, Month = 6, Year = 2026, LimitAmount = 0 };
        SetupSingleBudget(budget);
        _transactionsRepo.Setup(r => r.GetExpenseTotalByCategoryAsync(AccountId, 2, 6, 2026)).ReturnsAsync(10m);

        var item = (await _service.GetByAccountIdAsync(AccountId, 1, AccountId)).Items.First();

        Assert.That(item!.Percentage, Is.EqualTo(0m));
        Assert.That(item.IsLimit, Is.True);   // any spend exceeds a zero budget
    }

    [Test]
    public async Task GetByAccountIdAsync_NoSpend_IsZeroPercentAndNotOverLimit()
    {
        var budget = new BudgetLimit { Id = 1, AccountId = AccountId, CategoryId = 2, Month = 6, Year = 2026, LimitAmount = 100 };
        SetupSingleBudget(budget);
        _transactionsRepo.Setup(r => r.GetExpenseTotalByCategoryAsync(AccountId, 2, 6, 2026)).ReturnsAsync(0m);

        var item = (await _service.GetByAccountIdAsync(AccountId, 1, AccountId)).Items.First();

        Assert.That(item!.Percentage, Is.EqualTo(0m));
        Assert.That(item.IsLimit, Is.False);
    }

    [Test]
    public async Task Dado_LimiteNegativo_Quando_ListarPorConta_Entao_PercentualEhPositivoEListaLimiteAbsoluto()
    {
        var budget = new BudgetLimit
        {
            Id = 1,
            AccountId = AccountId,
            CategoryId = 6,
            Month = 6,
            Year = 2026,
            LimitAmount = -100m,
            Category = new Category { Name = "Lazer" },
        };
        SetupSingleBudget(budget);
        _transactionsRepo.Setup(r => r.GetExpenseTotalByCategoryAsync(AccountId, 6, 6, 2026)).ReturnsAsync(50m);

        var result = await _service.GetByAccountIdAsync(AccountId, 1, AccountId);
        var item = result.Items.First();

        Assert.Multiple(() =>
        {
            Assert.That(item.LimitAmount, Is.EqualTo(100m));
            Assert.That(item.SpentAmount, Is.EqualTo(50m));
            Assert.That(item.Percentage, Is.EqualTo(50m));
            Assert.That(item.IsLimit, Is.False);
        });
    }
}
