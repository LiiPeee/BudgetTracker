using BudgetTracker.Application.Dtos.Request;
using BudgetTracker.Application.Service;
using BudgetTracker.Core.Domain.Dtos.Request.Transaction;
using BudgetTracker.Core.Domain.Enum;
using BudgetTracker.Infrastructure.Persistence.Repository;
using Dapper;
using Microsoft.Extensions.Configuration;
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
        INSERT INTO Category(Name, IsActive, CreatedAt) VALUES ('MORADIA', true, now());
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
