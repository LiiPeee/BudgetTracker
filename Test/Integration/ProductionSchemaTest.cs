using BudgetTracker.Application.Dtos.Request;
using BudgetTracker.Application.Service;
using BudgetTracker.Core.Domain.Dtos.Request.Transaction;
using BudgetTracker.Core.Domain.Enum;
using BudgetTracker.Core.Domain.Models.Request.Account;
using BudgetTracker.Core.Domain.Models.Request.Stock;
using BudgetTracker.Core.Domain.Utils;
using BudgetTracker.Core.Infrastructure.Services;
using BudgetTracker.Infrastructure.Persistence.Repository;
using Dapper;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Moq;
using Npgsql;

namespace Test.Integration;

/// <summary>
/// Roda os serviços sobre a estrutura real do banco (db/migrations). Só executa quando
/// BUDGETTRACKER_TEST_DB aponta para um PostgreSQL descartável.
/// </summary>
[TestFixture]
public class ProductionSchemaTest
{
    // Registros de apoio supostos pelo código; os de produção ainda não estão versionados.
    private const string ReferenceData = """
        INSERT INTO recurrence(id, name) VALUES (1,'NONE'),(2,'DAILY'),(3,'BIWEEKLY'),(4,'MONTHLY'),(5,'OCCASIONALLY');
        INSERT INTO typecontact(id, name) VALUES (1,'PERSONAL'),(2,'BUSINESS');
        INSERT INTO typetransaction(id, name) VALUES (1,'EXPENSE'),(2,'INCOME');
        INSERT INTO category(name) VALUES ('MORADIA'),('ALIMENTACAO');
        INSERT INTO account(firstname, lastname, email, password) VALUES ('a','a','a@example.test','x'),('b','b','b@example.test','x');
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

        _schemaName = $"it_{Guid.NewGuid():N}";
        _connectionString = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = _schemaName }.ConnectionString;
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:BudgetTracker"] = _connectionString,
                ["Jwt:Token"] = new string('k', 64),
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test",
                ["Jwt:TokenExpirationMinutes"] = "5",
                ["Jwt:RefreshTokenExpirationMinutes"] = "5",
            })
            .Build();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync($"CREATE SCHEMA {_schemaName};");

        // As migrações em ordem, como em um banco novo.
        foreach (var script in Directory.GetFiles(MigrationsDirectory(), "*.sql").Order())
            await connection.ExecuteAsync(await File.ReadAllTextAsync(script));

        await connection.ExecuteAsync(ReferenceData);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_connectionString is null) return;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync($"DROP SCHEMA IF EXISTS {_schemaName} CASCADE;");
    }

    [Test]
    public async Task GoogleSignIn_CreatesAnAccountWithoutPassword()
    {
        using var session = new DbSession(_configuration);
        var service = new GoogleStubAuthService(session, _configuration);

        var tokens = await service.SignInGoogleAsync(new GoogleLoginRequestDto("token"));

        Assert.That(tokens?.AccessToken, Is.Not.Empty);
    }

    [Test]
    public async Task Subcategory_NameCanRepeat_AcrossAccountsAndCategories()
    {
        foreach (var account in new long[] { 1, 2 })
            await CreateContactAsync(account);

        await CreateTransactionAsync(1, "MORADIA");
        await CreateTransactionAsync(2, "MORADIA");
        await CreateTransactionAsync(1, "ALIMENTACAO");
        await CreateTransactionAsync(1, "MORADIA");

        Assert.That(await CountAsync("subcategory WHERE name = 'Geral'"), Is.EqualTo(3), "uma por conta e categoria");
    }

    [Test]
    public async Task FixedIncome_AcceptsASecondDeposit_ButAStockKeepsOnePosition()
    {
        await BuyAsync("CDB-X", isStock: false);
        await BuyAsync("CDB-X", isStock: false);
        await BuyAsync("PETR4", isStock: true);
        await BuyAsync("PETR4", isStock: true);

        Assert.That(await CountAsync("stock WHERE ticker = 'CDB-X'"), Is.EqualTo(2));
        Assert.That(await CountAsync("stock WHERE ticker = 'PETR4' AND quantity = 2"), Is.EqualTo(1));

        // O banco continua impedindo duas posições da mesma ação.
        await using var connection = new NpgsqlConnection(_connectionString);
        Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "INSERT INTO stock(accountid, ticker, title, pricebuyed, isstock) VALUES (1, 'PETR4', 't', 1, true)"));
    }

    private static string MigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BudgetTracker.sln")))
            directory = directory.Parent;

        return Path.Combine(directory!.FullName, "db", "migrations");
    }

    private async Task<long> CountAsync(string from)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<long>($"SELECT COUNT(1) FROM {from}");
    }

    private async Task CreateContactAsync(long accountId)
    {
        using var session = new DbSession(_configuration);
        await new ContactAppService(new ContactRepository(session), new AddressRepository(session), new UnitOfWork(session))
            .CreateAsync(accountId, new CreateContactRequest
            {
                Name = "Contato", Email = "c@example.test", Phone = "1", Document = "1", TypeContact = TypeContact.PERSONAL,
                Street = "s", City = "c", State = "s", ZipCode = "z", Country = "BR",
            });
    }

    private async Task CreateTransactionAsync(long accountId, string category)
    {
        using var session = new DbSession(_configuration);
        var service = new TransactionsAppService(new TransactionsRepository(session), new CategoryRepository(session),
            new ContactRepository(session), new SubCategoryRepository(session), new AccountRepository(session), new UnitOfWork(session));

        await service.CreateAsync(accountId, new CreateTrasactionRequest
        {
            Amount = 10, TransactionName = "Compra", Description = "", CategoryName = category, SubCategoryName = "Geral",
            ContactName = "Contato", TypeTransaction = TypeTransactions.EXPENSE, Recurrence = Recurrence.NONE,
        });
    }

    private async Task BuyAsync(string ticker, bool isStock)
    {
        using var session = new DbSession(_configuration);
        var market = new Mock<IStockMarketService>();
        market.Setup(m => m.GetStockByTickerAsync(It.IsAny<List<string>>())).ReturnsAsync([]);

        await new StockAppService(new StockRepository(session), market.Object, new UnitOfWork(session))
            .CreateAsync(1, new CreateStockRequest { Ticker = ticker, Title = "Titulo", Price = 10, Quantity = 1, IsStock = isStock });
    }

    // Só a validação do token com o Google é substituída; o resto é o caminho real.
    private sealed class GoogleStubAuthService(DbSession session, IConfiguration configuration) : AuthenticationAppService(
        new AccountRepository(session), new ResetPasswordRepository(session), RefreshTokens(), new PasswordHasherService(),
        Mock.Of<IEmailService>(), new UnitOfWork(session), configuration, new AuthAttemptTracker())
    {
        private static IPasswordHelper RefreshTokens()
        {
            var helper = new Mock<IPasswordHelper>();
            helper.Setup(h => h.GenerateRefreshToken()).Returns("refresh");
            return helper.Object;
        }

        protected override Task<GoogleJsonWebSignature.Payload> ValidateGoogleTokenAsync(string idToken) =>
            Task.FromResult(new GoogleJsonWebSignature.Payload
            {
                Email = "novo@example.test", EmailVerified = true, GivenName = "Nome", FamilyName = "Sobrenome",
            });
    }
}
