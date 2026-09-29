using BudgetTracker.Application.Service;
using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Infrastructure.Repository;
using Moq;

namespace Test.Application;

/// <summary>
/// A budget's usage % must reflect spent/limit even when a legacy row stored a
/// negative LimitAmount — the service normalizes it to the absolute value instead
/// of collapsing the percentage to 0.
/// </summary>
[TestFixture]
public class BudgetLimitServiceTests
{
    private const long AccountId = 1;

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

        var budgetRepository = new Mock<IBudgetLimitRepository>();
        budgetRepository
            .Setup(r => r.GetByAccountIdAsync(AccountId, It.IsAny<int>(), AccountId, It.IsAny<int>()))
            .ReturnsAsync(new IPagedResult<BudgetLimit?>
            {
                PageNumber = 1,
                PageSize = 10,
                TotalRecords = 1,
                Items = new List<BudgetLimit?> { budget },
            });
        budgetRepository.Setup(r => r.UpdateAsync(It.IsAny<BudgetLimit>())).ReturnsAsync(true);

        var transactionRepository = new Mock<ITransactionsRepository>();
        transactionRepository
            .Setup(r => r.GetExpenseTotalByCategoryAsync(AccountId, 6, 6, 2026))
            .ReturnsAsync(50m);

        var service = new BudgetLimitService(
            budgetRepository.Object,
            new Mock<IUnitOfWork>().Object,
            new Mock<ICategoryRepository>().Object,
            transactionRepository.Object);

        var result = await service.GetByAccountIdAsync(AccountId, 1, AccountId);
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
