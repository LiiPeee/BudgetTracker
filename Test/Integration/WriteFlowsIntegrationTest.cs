using BudgetTracker.Application.Dtos.Request;
using BudgetTracker.Application.Service;
using BudgetTracker.Core.Domain.Dtos.Request.Transaction;
using BudgetTracker.Core.Domain.Enum;
using BudgetTracker.Core.Domain.Models.Request.BudgetLimit;
using BudgetTracker.Core.Domain.Models.Request.Contact;
using BudgetTracker.Core.Domain.Models.Request.Stock;
using BudgetTracker.Core.Domain.Models.Request.Transaction;
using BudgetTracker.Core.Infrastructure.OutPut;
using BudgetTracker.Core.Infrastructure.Services;
using BudgetTracker.Infrastructure.Persistence.Repository;
using Dapper;
using Microsoft.Extensions.Configuration;
using Moq;
using Npgsql;

namespace Test.Integration;

/// <summary>
/// Executa gravações contra um PostgreSQL de verdade. Só roda quando a variável
/// BUDGETTRACKER_TEST_DB tiver a connection string de um banco descartável.
/// O schema abaixo foi derivado das entidades, não do banco de produção.
/// </summary>
[TestFixture]
public class WriteFlowsIntegrationTest
{
    private const long AccountId = 1;

    private const string Schema = """
        CREATE TABLE Category(Id bigserial PRIMARY KEY, Name text, Description text, IsActive bool, CreatedAt timestamp, UpdatedAt timestamp);
        CREATE TABLE SubCategory(Id bigserial PRIMARY KEY, Name text, Description text, IsActive bool, CategoryId bigint, AccountId bigint, CreatedAt timestamp, UpdatedAt timestamp);
        CREATE TABLE Contact(Id bigserial PRIMARY KEY, AccountId bigint, Name text, Email text, Phone text, Document text, IsActive bool, TypeContactId bigint, CreatedAt timestamp, UpdatedAt timestamp);
        CREATE TABLE Address(Id bigserial PRIMARY KEY, Street text, City text, State text, ZipCode text, Country text, IsPrimary bool, ContactId bigint, AccountId bigint, CreatedAt timestamp, UpdatedAt timestamp);
        CREATE TABLE Transactions(Id bigserial PRIMARY KEY, AccountId bigint, Amount numeric, Name text, Description text, Paid bool,
            NumberOfInstallment bigint, DateOfInstallment timestamp, CompetenceDate timestamp, QuantityInstallment text, RecurrenceId bigint,
            ContactId bigint, SubCategoryId bigint, CategoryId bigint, TypeTransactionId bigint, CreatedAt timestamp, UpdatedAt timestamp);
        CREATE TABLE Account(Id bigserial PRIMARY KEY, FirstName text, LastName text, Email text, Password text, Balance numeric, Role text,
            RefreshToken text, RefreshTokenExpiryTime timestamptz, EmailVerified bool, VerifiedAt timestamp, EmailVerificationToken text,
            EmailVerificationTokenExpiry timestamp, VerifyAttempts bigint DEFAULT 0, LoginAttempts bigint DEFAULT 0, IsActive bool, CreatedAt timestamp, UpdatedAt timestamp);
        CREATE TABLE BudgetLimit(Id bigserial PRIMARY KEY, IsLimit bool, Month int, Year int, CategoryId bigint, Percentage numeric, AccountId bigint, LimitAmount numeric, CreatedAt timestamp, UpdatedAt timestamp);
        CREATE TABLE Stock(Id bigserial PRIMARY KEY, AccountId bigint, Ticker text, Description text, Title text, IsStock bool, FixedIncomeType text,
            CdiRate numeric, InvestmentDate timestamp, PriceBuyed numeric, PriceMarket numeric DEFAULT 0, Avarage text, Quantity numeric, CreatedAt timestamp, UpdatedAt timestamp);
        CREATE TABLE TypeTransaction(Id bigint PRIMARY KEY, Name text);
        INSERT INTO TypeTransaction VALUES (1, 'EXPENSE'), (2, 'INCOME');
        INSERT INTO Category(Name, IsActive, CreatedAt) VALUES ('MORADIA', true, now()), ('Alimentação', true, now()), ('Bens Pessoais', true, now());
        INSERT INTO Account(Email, IsActive, CreatedAt) VALUES ('Pessoa@Example.com', true, now());
        """;

    private string _schemaName = null!;
    private string _connectionString = null!;
    private IConfiguration _configuration = null!;

    [SetUp]
    public async Task SetUp()
    {
        var baseConnection = Environment.GetEnvironmentVariable("BUDGETTRACKER_TEST_DB");
        if (string.IsNullOrWhiteSpace(baseConnection))
            Assert.Ignore("Defina BUDGETTRACKER_TEST_DB para rodar os testes de integração.");

        // Schema próprio e descartável: nada que já exista no banco é tocado.
        _schemaName = $"it_{Guid.NewGuid():N}";
        _connectionString = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = _schemaName }.ConnectionString;
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:BudgetTracker"] = _connectionString })
            .Build();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync($"CREATE SCHEMA {_schemaName};");
        await connection.ExecuteAsync(Schema);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_connectionString is null) return;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync($"DROP SCHEMA IF EXISTS {_schemaName} CASCADE;");
    }

    // Cada operação usa uma sessão nova, como cada requisição da API.
    private async Task WithContactService(Func<ContactAppService, Task> action)
    {
        using var session = new DbSession(_configuration);
        await action(new ContactAppService(new ContactRepository(session), new AddressRepository(session), new UnitOfWork(session)));
    }

    private async Task WithTransactionService(Func<TransactionsAppService, Task> action)
    {
        using var session = new DbSession(_configuration);
        await action(new TransactionsAppService(
            new TransactionsRepository(session),
            new CategoryRepository(session),
            new ContactRepository(session),
            new SubCategoryRepository(session),
            new AccountRepository(session),
            new UnitOfWork(session)));
    }

    private static CreateContactRequest NewContact(string name) => new()
    {
        Name = name, Email = "c@example.com", Phone = "1", Document = "1", TypeContact = TypeContact.PERSONAL,
        Street = "Rua A", City = "c", State = "s", ZipCode = "z", Country = "BR", IsPrimary = true,
    };

    private static CreateTrasactionRequest NewTransaction(string category) => new()
    {
        Amount = 10, TransactionName = "Compra", Description = "", CategoryName = category,
        SubCategoryName = "Geral", ContactName = "Contato", Paid = true,
        TypeTransaction = TypeTransactions.EXPENSE, Recurrence = Recurrence.NONE,
    };

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        return (await connection.ExecuteScalarAsync<T>(sql))!;
    }

    [Test]
    public async Task EditContact_KeepsCreationDate_SavesAddress_AndNeverRevivesADeletedOne()
    {
        await WithContactService(service => service.CreateAsync(AccountId, NewContact("Contato")));
        var id = await ScalarAsync<long>("SELECT Id FROM Contact");
        var createdAt = await ScalarAsync<DateTime>("SELECT CreatedAt FROM Contact");

        var edit = new ContactRequest
        {
            ContactId = id.ToString(), Name = "Contato", Email = "n@example.com", Phone = "2", Document = "2",
            TypeContact = TypeContact.BUSINESS, Street = "Rua B", City = "c", State = "s", ZipCode = "z", Country = "BR",
        };
        await WithContactService(service => service.EditContactAsync(AccountId, edit));

        Assert.That(await ScalarAsync<DateTime>("SELECT CreatedAt FROM Contact"), Is.EqualTo(createdAt));
        await WithContactService(async service =>
        {
            var listed = (await service.GetAllsync(AccountId)).Single()!;
            Assert.That((listed.Street, listed.TypeContactId), Is.EqualTo(("Rua B", 2L)));
        });
        Assert.That(await CountAsync("Address"), Is.EqualTo(1));

        Assert.ThrowsAsync<ArgumentException>(() => WithContactService(s => s.CreateAsync(AccountId, NewContact("Contato"))));

        await WithContactService(service => service.DeleteContactAsync(AccountId, id.ToString()));
        Assert.ThrowsAsync<KeyNotFoundException>(() => WithContactService(s => s.EditContactAsync(AccountId, edit)));
        Assert.That(await CountAsync("Contact WHERE IsActive"), Is.Zero);
    }

    [Test]
    public async Task Transactions_MatchCategoryIgnoringAccents_AndStayEditableAfterContactIsDeleted()
    {
        await WithContactService(service => service.CreateAsync(AccountId, NewContact("Contato")));

        foreach (var category in new[] { "ALIMENTACAO", "BENS_PESSOAIS", "Moradia" })
            await WithTransactionService(service => service.CreateAsync(AccountId, NewTransaction(category)));

        var now = BudgetTracker.Core.Domain.Utils.AppClock.Now;
        foreach (var category in new[] { "ALIMENTACAO", "BENS_PESSOAIS", "MORADIA" })
            await WithTransactionService(async service =>
            {
                var page = await service.FilterTransactionsByCategoryAsync(AccountId, category, BudgetTracker.Core.Domain.Entities.TypeTransaction.EXPENSE, now.Month, now.Year);
                Assert.That(page.TotalRecords, Is.EqualTo(1), category);
            });

        var contactId = await ScalarAsync<long>("SELECT Id FROM Contact");
        await WithContactService(service => service.DeleteContactAsync(AccountId, contactId.ToString()));

        var transactionId = await ScalarAsync<long>("SELECT min(Id) FROM Transactions");
        await WithTransactionService(service => service.EditTransactionAsync(AccountId, transactionId, new EditTransactionRequest
        {
            Amount = 20, TransactionName = "Compra", CategoryName = "Moradia", SubCategoryName = "Geral",
            ContactName = "Contato", TypeTransaction = TypeTransactions.EXPENSE, Paid = true,
        }));
        Assert.That(await CountAsync("Transactions WHERE Amount = 20"), Is.EqualTo(1));

        Assert.ThrowsAsync<KeyNotFoundException>(() => WithTransactionService(s => s.DeleteAsync(AccountId, 999999)));
    }

    [Test]
    public async Task Lists_ShowEveryTransactionTheTotalsCount_EvenWithMissingData()
    {
        // Registros antigos podem não ter contato, categoria ou subcategoria.
        await ScalarAsync<int>(@"INSERT INTO Contact(AccountId, Name, IsActive, TypeContactId, CreatedAt) VALUES (1, 'Contato', true, 1, now());
            INSERT INTO SubCategory(Name, IsActive, CategoryId, AccountId, CreatedAt) VALUES ('Geral', true, 1, 1, now()); SELECT 1");
        await ScalarAsync<int>(@"INSERT INTO Transactions(AccountId, Amount, Name, Paid, CompetenceDate, RecurrenceId, ContactId, CategoryId, SubCategoryId, TypeTransactionId, CreatedAt)
            VALUES (1, 1, 'completa',         false, '2026-11-05', 4, 1,    1,    1,    1, now()),
                   (1, 2, 'sem subcategoria', false, '2026-11-05', 4, 1,    1,    NULL, 1, now()),
                   (1, 4, 'sem contato',      false, '2026-11-05', 4, NULL, 1,    1,    1, now()),
                   (1, 8, 'sem categoria',    false, '2026-11-05', 4, 1,    NULL, 1,    1, now()); SELECT 1");

        await WithTransactionService(async service =>
        {
            var type = BudgetTracker.Core.Domain.Entities.TypeTransaction.EXPENSE;
            var total = await service.FilterExpenseMonthAndYearAsync(AccountId, 2026, 11);
            var lists = new[]
            {
                await service.FilterByMonthAndYearAsync(AccountId, 11, 2026),
                await service.FilterTransactionByTypeAsync(AccountId, type, 11, 2026),
                await service.FilterAllInstallmentsAsync(AccountId, 11, 2026, type),
                await service.FilterTransactionByPaidAsync(AccountId, 11, 2026, false),
            };

            Assert.That(total, Is.EqualTo(15m));
            foreach (var list in lists)
                Assert.That((list.Items.Sum(i => i.Amount), list.TotalRecords), Is.EqualTo((15m, 4)));

            // Os filtros por contato e categoria continuam trazendo só o que corresponde.
            var byContact = await service.FilterByContactAndMonth(AccountId, 2026, 11, type, 1);
            var byCategory = await service.FilterTransactionsByCategoryAsync(AccountId, "MORADIA", type, 11, 2026);
            Assert.That((byContact.TotalRecords, byContact.Items.Sum(i => i.Amount)), Is.EqualTo((3, 11m)));
            Assert.That((byCategory.TotalRecords, byCategory.Items.Sum(i => i.Amount)), Is.EqualTo((3, 7m)));
        });
    }

    [Test]
    public async Task Budgets_RejectDuplicates_AndCountOnlyTheRequestedMonth()
    {
        async Task With(Func<BudgetLimitService, Task> action)
        {
            using var session = new DbSession(_configuration);
            await action(new BudgetLimitService(new BudgetLimitRepository(session), new UnitOfWork(session),
                new CategoryRepository(session), new TransactionsRepository(session)));
        }

        CreateBudgetLimit Budget(int month) => new() { AccountId = AccountId, CategoryName = "ALIMENTACAO", Month = month, Year = 2026, LimitAmount = 100 };

        await With(service => service.CreateAsync(Budget(6)));
        await With(service => service.CreateAsync(Budget(7)));
        Assert.ThrowsAsync<ArgumentException>(() => With(s => s.CreateAsync(Budget(6))));

        await With(async service =>
        {
            var page = await service.GetByAccountIdAsync(6, 2026, AccountId);
            Assert.That((page.Items.Count(), page.TotalRecords), Is.EqualTo((1, 1)));
        });
    }

    [Test]
    public async Task Stocks_QuoteTheFirstBuy_AccumulateShares_AndKeepEachFixedIncomeDeposit()
    {
        var market = new Mock<IStockMarketService>();
        market.Setup(m => m.GetStockByTickerAsync(It.IsAny<List<string>>()))
            .ReturnsAsync([new StockMarketResponse { Ticker = "PETR4", PriceMarket = 30 }]);

        async Task Buy(string ticker, bool isStock, decimal price = 20)
        {
            using var session = new DbSession(_configuration);
            await new StockAppService(new StockRepository(session), market.Object, new UnitOfWork(session))
                .CreateAsync(AccountId, new CreateStockRequest { Ticker = ticker, Title = "Titulo", Price = price, Quantity = 1, IsStock = isStock });
        }

        await Buy("petr4", true);
        Assert.That(await ScalarAsync<decimal>("SELECT PriceMarket FROM Stock"), Is.EqualTo(30m));
        var createdAt = await ScalarAsync<DateTime>("SELECT CreatedAt FROM Stock");

        market.Setup(m => m.GetStockByTickerAsync(It.IsAny<List<string>>())).ReturnsAsync([]);
        await Buy("PETR4", true);
        Assert.That(await CountAsync("Stock WHERE Quantity = 2 AND PriceMarket = 30"), Is.EqualTo(1), "compra sem cotação não se perde");
        Assert.That(await ScalarAsync<DateTime>("SELECT CreatedAt FROM Stock"), Is.EqualTo(createdAt));

        await Buy("CDB-X", false);
        await Buy("CDB-X", false);
        Assert.That(await CountAsync("Stock WHERE NOT IsStock"), Is.EqualTo(2));

        Assert.ThrowsAsync<ArgumentException>(() => Buy("NEG", true, -1));
    }

    [Test]
    public async Task Login_FindsTheAccountIgnoringEmailCase()
    {
        using var session = new DbSession(_configuration);

        Assert.That(await new AccountRepository(session).GetByEmailAsync("pessoa@example.com"), Is.Not.Null);
    }

    private async Task<long> CountAsync(string where)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<long>($"SELECT COUNT(1) FROM {where}");
    }

    [Test]
    public async Task CreateContact_CreateTransaction_AndDeleteIt()
    {
        await WithContactService(service => service.CreateAsync(AccountId, new CreateContactRequest
        {
            Name = "Contato", Email = "c@example.com", Phone = "1", Document = "1", TypeContact = TypeContact.PERSONAL,
            Street = "s", City = "c", State = "s", ZipCode = "z", Country = "BR", IsPrimary = true,
        }));
        Assert.That(await CountAsync("Contact"), Is.EqualTo(1));

        long id = 0;
        await WithTransactionService(async service =>
        {
            var created = await service.CreateAsync(AccountId, new CreateTrasactionRequest
            {
                Amount = 10, TransactionName = "Aluguel", Description = "casa", CategoryName = "MORADIA",
                SubCategoryName = "Aluguel", ContactName = "Contato", Paid = true,
                TypeTransaction = TypeTransactions.EXPENSE, Recurrence = Recurrence.NONE,
            });
            id = created.Single().Id;
        });
        Assert.That(await CountAsync("Transactions WHERE Paid"), Is.EqualTo(1));

        await WithTransactionService(service => service.DeleteAsync(AccountId, id));
        Assert.That(await CountAsync("Transactions"), Is.Zero);
    }
}
