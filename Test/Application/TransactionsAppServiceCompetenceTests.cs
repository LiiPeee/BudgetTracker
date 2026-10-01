using BudgetTracker.Application.Service;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Enum;
using BudgetTracker.Core.Domain.Dtos.Request.Transaction;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Infrastructure.Repository;
using Moq;

namespace Test.Application;

/// <summary>
/// Locks the competence-month semantics: a transaction's financial month lives in
/// <see cref="Transactions.CompetenceDate"/>, never in the audit timestamp <c>CreatedAt</c>.
/// </summary>
[TestFixture]
public class TransactionsAppServiceCompetenceTests
{
    private const long AccountId = 1;
    private const int ContactId = 10;
    private const int SubCategoryId = 5;

    private Mock<ITransactionsRepository> _transactionRepository = null!;
    private Mock<ICategoryRepository> _categoryRepository = null!;
    private Mock<IContactRepository> _contactRepository = null!;
    private Mock<ISubCategoryRepository> _subCategoryRepository = null!;
    private Mock<IAccountRepository> _accountRepository = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private List<Transactions> _saved = null!;
    private TransactionsAppService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _transactionRepository = new Mock<ITransactionsRepository>();
        _categoryRepository = new Mock<ICategoryRepository>();
        _contactRepository = new Mock<IContactRepository>();
        _subCategoryRepository = new Mock<ISubCategoryRepository>();
        _accountRepository = new Mock<IAccountRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();

        _contactRepository
            .Setup(r => r.GetByNameAsync(AccountId, It.IsAny<string>()))
            .ReturnsAsync(new Contact { Id = ContactId, Name = "Mercado", AccountId = AccountId });

        _categoryRepository
            .Setup(r => r.GetByNameAsync(It.IsAny<string>()))
            .ReturnsAsync(new Category { Id = 2, Name = "Alimentação" });

        _subCategoryRepository
            .Setup(r => r.GetByNameAsync(AccountId, It.IsAny<string>(), It.IsAny<long?>()))
            .ReturnsAsync(new SubCategory { Id = SubCategoryId, Name = "Sub", AccountId = AccountId, IsActive = true });

        _saved = new List<Transactions>();
        _transactionRepository
            .Setup(r => r.AddAsync(It.IsAny<Transactions>()))
            .ReturnsAsync((Transactions t) =>
            {
                _saved.Add(t);
                return t;
            });

        _service = new TransactionsAppService(
            _transactionRepository.Object,
            _categoryRepository.Object,
            _contactRepository.Object,
            _subCategoryRepository.Object,
            _accountRepository.Object,
            _unitOfWork.Object);
    }

    private static CreateTrasactionRequest Request(Recurrence recurrence, int? installments = null, int? dayOfInstallment = null) => new()
    {
        Amount = 100m,
        TransactionName = "Compra",
        Description = "Teste",
        CategoryName = "Lazer",
        SubCategoryName = "Sub",
        ContactName = "Mercado",
        TypeTransaction = TypeTransactions.EXPENSE,
        Paid = false,
        Recurrence = recurrence,
        NumberOfInstallment = installments,
        DateOfInstallment = dayOfInstallment,
    };

    [Test]
    public async Task Dado_RecorrenciaNONE_Quando_Criar_Entao_CompetenceDate_EhMesAtual()
    {
        var expected = DateTime.UtcNow;

        await _service.CreateAsync(AccountId, Request(Recurrence.NONE));

        var transaction = _saved.Single();
        Assert.Multiple(() =>
        {
            Assert.That(transaction.CompetenceDate.Month, Is.EqualTo(expected.Month));
            Assert.That(transaction.CompetenceDate.Year, Is.EqualTo(expected.Year));
        });
    }

    [Test]
    public async Task Dado_RecorrenciaOCCASIONALLY_Quando_Criar_Entao_CompetenceDate_EhProximoMes()
    {
        var expected = DateTime.UtcNow.AddMonths(1);

        await _service.CreateAsync(AccountId, Request(Recurrence.OCCASIONALLY));

        var transaction = _saved.Single();
        Assert.Multiple(() =>
        {
            Assert.That(transaction.CompetenceDate.Month, Is.EqualTo(expected.Month));
            Assert.That(transaction.CompetenceDate.Year, Is.EqualTo(expected.Year));
        });
    }

    [Test]
    public async Task Dado_QualquerRecorrencia_Quando_Criar_Entao_CreatedAt_PermaneceMesAtual()
    {
        var now = DateTime.UtcNow;

        await _service.CreateAsync(AccountId, Request(Recurrence.OCCASIONALLY));

        // CreatedAt is audit-only — never pushed forward, even for OCCASIONALLY.
        Assert.That(_saved.Single().CreatedAt.Month, Is.EqualTo(now.Month));
    }

    [Test]
    public async Task Dado_Parcelas_Quando_Criar_Entao_CompetenceDate_AcompanhaDataDaParcela()
    {
        await _service.CreateAsync(AccountId, Request(Recurrence.MONTHLY, installments: 3, dayOfInstallment: 5));

        Assert.That(_saved, Has.Count.EqualTo(3));
        foreach (var transaction in _saved)
        {
            // Installments anchor on DateOfInstallment; competence must match it.
            Assert.That(transaction.CompetenceDate, Is.EqualTo(transaction.DateOfInstallment));
        }
    }

    [Test]
    public async Task Dado_ParcelasComOCCASIONALLY_Quando_Criar_Entao_PushNaoSeAplica()
    {
        // Installments ignore the OCCASIONALLY push: first installment is next month
        // (AddMonths(1) from the installment loop), NOT two months out.
        var firstInstallment = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 5).AddMonths(1);

        await _service.CreateAsync(AccountId, Request(Recurrence.OCCASIONALLY, installments: 2, dayOfInstallment: 5));

        var first = _saved.First();
        Assert.Multiple(() =>
        {
            Assert.That(first.CompetenceDate.Month, Is.EqualTo(firstInstallment.Month));
            Assert.That(first.CompetenceDate.Year, Is.EqualTo(firstInstallment.Year));
        });
    }
}
