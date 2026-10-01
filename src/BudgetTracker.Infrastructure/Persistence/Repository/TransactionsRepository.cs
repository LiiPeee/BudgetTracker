using Dapper;
using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Enum;
using BudgetTracker.Core.Domain.Repository;
using System.Data;

namespace BudgetTracker.Infrastructure.Persistence.Repository;

public class TransactionsRepository : AccountScopedRepositoryBase<Transactions>, ITransactionsRepository
{
    public TransactionsRepository(DbSession connection) : base(connection)
    {
    }

    // Colunas explícitas para o multi-mapping (splitOn "Id,Id"): a ordem é fixa,
    // independente da ordem física das colunas no Postgres. Com `SELECT *` qualquer
    // alteração de schema poderia quebrar o mapeamento silenciosamente.
    private const string TransactionWithContactAndCategoryColumns = @"
        t.Id, t.AccountId, t.Amount, t.Name, t.Description, t.Paid,
        t.NumberOfInstallment, t.DateOfInstallment, t.CompetenceDate,
        t.QuantityInstallment, t.RecurrenceId, t.ContactId, t.SubCategoryId,
        t.CategoryId, t.TypeTransactionId, t.CreatedAt, t.UpdatedAt,
        ct.Id, ct.AccountId, ct.Name, ct.Email, ct.Phone, ct.Document,
        ct.IsActive, ct.TypeContactId, ct.CreatedAt, ct.UpdatedAt,
        cat.Id, cat.Name, cat.Description, cat.IsActive, cat.CreatedAt, cat.UpdatedAt";

    private const string TransactionWithContactColumns = @"
        t.Id, t.AccountId, t.Amount, t.Name, t.Description, t.Paid,
        t.NumberOfInstallment, t.DateOfInstallment, t.CompetenceDate,
        t.QuantityInstallment, t.RecurrenceId, t.ContactId, t.SubCategoryId,
        t.CategoryId, t.TypeTransactionId, t.CreatedAt, t.UpdatedAt,
        ct.Id, ct.AccountId, ct.Name, ct.Email, ct.Phone, ct.Document,
        ct.IsActive, ct.TypeContactId, ct.CreatedAt, ct.UpdatedAt";

    // FILTRO POR CATEGORIA, TIPO, MES E ANO
    public async Task<IPagedResult<Transactions>> FilterTransactionsByCategoryAsync(long accountId, string categoryName, string type, long month, long year, int pageNumber = 1)
    {
        const int pageSize = 10;

        pageNumber = Math.Max(1, pageNumber);
        var offset = (pageNumber - 1) * pageSize;

        var query = @"
        SELECT {TransactionWithContactAndCategoryColumns}
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        INNER JOIN TypeTransaction tp ON t.TypeTransactionId = tp.Id
        WHERE t.AccountId = @AccountId AND tp.Name = @Type AND cat.Name = @Category 
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year)
        ORDER BY t.Id DESC
        LIMIT @PageSize OFFSET @OffSet;

        SELECT COUNT(1)
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        INNER JOIN TypeTransaction tp ON t.TypeTransactionId = tp.Id
        WHERE t.AccountId = @AccountId AND tp.Name = @Type AND cat.Name = @Category 
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year);";

        if (_db._connection.State != ConnectionState.Open)
        {
            throw new Exception("connection lost");
        }

        using var multi = await _db._connection.QueryMultipleAsync(
            query,
            new { AccountId = accountId, Month = month, Year = year, OffSet = offset, PageSize = pageSize, Type = type, Category = categoryName },
            _db._transaction);

        var items = multi.Read<Transactions, Contact, Category, Transactions>(
            (t, c, cat) =>
            {
                t.Contact = c;
                t.Category = cat;
                return t;
            },
            splitOn: "Id,Id").ToList();

        var totalRecords = (int)await multi.ReadSingleAsync<long>();

        return new IPagedResult<Transactions>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            Items = items
        };
    }

    public async Task<IPagedResult<Transactions>> FilterTransactionsByTypeAsync(long accountId, string type, long month, long year, int pageNumber = 1)
    {
        const int pageSize = 10;

        pageNumber = Math.Max(1, pageNumber);
        var offset = (pageNumber - 1) * pageSize;

        var query = @"
        SELECT {TransactionWithContactAndCategoryColumns}
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        INNER JOIN TypeTransaction tp ON t.TypeTransactionId = tp.Id
        WHERE t.AccountId = @AccountId AND tp.Name = @Type 
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year)
        ORDER BY t.Id DESC
        LIMIT @PageSize OFFSET @OffSet;

        SELECT COUNT(1)
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        INNER JOIN TypeTransaction tp ON t.TypeTransactionId = tp.Id
        WHERE t.AccountId = @AccountId AND tp.Name = @Type 
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year);";

        if (_db._connection.State != ConnectionState.Open)
        {
            throw new Exception("connection lost");
        }

        using var multi = await _db._connection.QueryMultipleAsync(
            query,
            new { AccountId = accountId, Month = month, Year = year, OffSet = offset, PageSize = pageSize, Type = type },
            _db._transaction);

        var items = multi.Read<Transactions, Contact, Category, Transactions>(
            (t, c, cat) =>
            {
                t.Contact = c;
                t.Category = cat;
                return t;
            },
            splitOn: "Id,Id").ToList();

        var totalRecords = (int)await multi.ReadSingleAsync<long>();

        return new IPagedResult<Transactions>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            Items = items
        };
    }

    public async Task<IPagedResult<Transactions>> FilterByMonthAndYearAsync(long accountId, long month, long year, int pageNumber = 1)
    {
        const int pageSize = 10;

        pageNumber = Math.Max(1, pageNumber);
        var offset = (pageNumber - 1) * pageSize;

        var query = @"
        SELECT {TransactionWithContactAndCategoryColumns}
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        WHERE t.AccountId = @AccountId 
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year)
        ORDER BY t.Id DESC
        LIMIT @PageSize OFFSET @OffSet;

        SELECT COUNT(1)
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        WHERE t.AccountId = @AccountId 
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year);";

        if (_db._connection.State != ConnectionState.Open)
        {
            throw new Exception("connection lost");
        }

        using var multi = await _db._connection.QueryMultipleAsync(
            query,
            new { AccountId = accountId, Month = month, Year = year, OffSet = offset, PageSize = pageSize },
            _db._transaction);

        var items = multi.Read<Transactions, Contact, Category, Transactions>(
            (t, c, cat) =>
            {
                t.Contact = c;
                t.Category = cat;
                return t;
            },
            splitOn: "Id,Id").ToList();

        var totalRecords = (int)await multi.ReadSingleAsync<long>();

        return new IPagedResult<Transactions>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            Items = items
        };
    }


    public async Task<IPagedResult<Transactions>> FilterAllInstallmentsAsync(long accountId, long month, long year, string type, int pageNumber = 1)
    {
        const int pageSize = 10;

        pageNumber = Math.Max(1, pageNumber);
        var offset = (pageNumber - 1) * pageSize;

        var query = @"
        SELECT {TransactionWithContactAndCategoryColumns}
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        INNER JOIN TypeTransaction tp ON t.TypeTransactionId = tp.Id
        WHERE t.AccountId = @AccountId
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month
            AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year)
            AND t.RecurrenceId = @RecurrenceId
            AND tp.Name = @Type
        ORDER BY t.Id DESC
        LIMIT @PageSize OFFSET @OffSet;

        SELECT COUNT(1)
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        INNER JOIN TypeTransaction tp ON t.TypeTransactionId = tp.Id
        WHERE t.AccountId = @AccountId
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year)
            AND t.RecurrenceId = @RecurrenceId
            AND tp.Name = @Type;";

        if (_db._connection.State != ConnectionState.Open)
        {
            throw new Exception("connection lost");
        }

        using var multi = await _db._connection.QueryMultipleAsync(
            query,
            new { AccountId = accountId, Month = month, Type = type, Year = year, RecurrenceId = (long)Recurrence.MONTHLY, OffSet = offset, PageSize = pageSize },
            _db._transaction);

        var items = multi.Read<Transactions, Contact, Category, Transactions>(
            (t, c, cat) =>
            {
                t.Contact = c;
                t.Category = cat;
                return t;
            },
            splitOn: "Id,Id").ToList();

        var totalRecords = (int)await multi.ReadSingleAsync<long>();

        return new IPagedResult<Transactions>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            Items = items
        };
    }


    public async Task<IPagedResult<Transactions>> FilterByContactAsync(long accountId, long year, long month, string type, long contactId, int pageNumber = 1)
    {

        const int pageSize = 10;

        pageNumber = Math.Max(1, pageNumber);
        var offset = (pageNumber - 1) * pageSize;

        var query = @"
        SELECT {TransactionWithContactAndCategoryColumns}
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        INNER JOIN TypeTransaction tp ON t.TypeTransactionId = tp.Id
        WHERE t.AccountId = @AccountId AND tp.Name = @Type 
            AND ct.Id = @ContactId
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year)
        ORDER BY t.Id DESC
        LIMIT @PageSize OFFSET @OffSet;

        SELECT COUNT(1)
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        INNER JOIN TypeTransaction tp ON t.TypeTransactionId = tp.Id
        WHERE t.AccountId = @AccountId AND tp.Name = @Type 
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year) AND ct.Id = @ContactId ;";

        if (_db._connection.State != ConnectionState.Open)
        {
            throw new Exception("connection lost");
        }

        using var multi = await _db._connection.QueryMultipleAsync(
            query,
            new { AccountId = accountId, Month = month, Year = year, OffSet = offset, PageSize = pageSize, ContactId = contactId, Type = type },
            _db._transaction);

        var items = multi.Read<Transactions, Contact, Category, Transactions>(
            (t, c, cat) =>
            {
                t.Contact = c;
                t.Category = cat;
                return t;
            },
            splitOn: "Id,Id").ToList();

        var totalRecords = (int)await multi.ReadSingleAsync<long>();

        return new IPagedResult<Transactions>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            Items = items
        };
    }

    public async Task<decimal> FilterExpenseMonthAndYearAsync(long accountId, long year, long month)
    {
        const string query = @"
        SELECT COALESCE(SUM(t.Amount), 0)
        FROM Transactions t
        WHERE t.AccountId = @AccountId
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year)
            AND t.TypeTransactionId = @TypeId";

        if (_db._connection.State != ConnectionState.Open)
            throw new Exception("connection lost");

        return await _db._connection.ExecuteScalarAsync<decimal>(query, new { AccountId = accountId, Month = month, Year = year, TypeId = (long)TypeTransactions.EXPENSE }, _db._transaction);
    }

    public async Task<decimal> FilterIncomeMonthAndYearAsync(long accountId, long year, long month)
    {
        const string query = @"
        SELECT COALESCE(SUM(t.Amount), 0)
        FROM Transactions t
        WHERE t.AccountId = @AccountId
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year)
            AND t.TypeTransactionId = @TypeId";

        if (_db._connection.State != ConnectionState.Open)
            throw new Exception("connection lost");

        return await _db._connection.ExecuteScalarAsync<decimal>(query, new { AccountId = accountId, Month = month, Year = year, TypeId = (long)TypeTransactions.INCOME }, _db._transaction);
    }

    public async Task<List<Transactions>> FilterExpenseMonthWithContactAsync(long accountId, long year, long month)
    {
        var query = @"SELECT {TransactionWithContactColumns}
        FROM Transactions t 
        LEFT JOIN Contact ct ON t.ContactId = ct.Id
        WHERE t.AccountId = @AccountId 
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year)";

        if (_db._connection.State == ConnectionState.Open)
        {
            var result = await _db._connection.QueryAsync<Transactions, Contact, Transactions>(query, (t, c) =>
            {
                t.Contact = c;
                return t;
            },
            new { AccountId = accountId, Month = month, Year = year }, transaction: _db._transaction, splitOn: "Id");

            return result.ToList();
        }
        else
        {
            throw new Exception("connection lost");
        }
    }

    public async Task<bool> MarkAsPaidAsync(long id, long accountId, bool paid)
    {
        const string query = @"UPDATE Transactions SET Paid = @Paid WHERE Id = @Id AND AccountId = @AccountId";

        if (_db._connection.State != ConnectionState.Open)
            throw new Exception("connection lost");

        var rows = await _db._connection.ExecuteAsync(query, new { Id = id, AccountId = accountId, Paid = paid }, _db._transaction);
        return rows > 0;
    }

    public async Task<decimal> GetExpenseTotalByCategoryAsync(long accountId, long categoryId, int month, int year)
    {
        const string query = @"
        SELECT COALESCE(SUM(t.Amount), 0)
        FROM Transactions t
        WHERE t.AccountId = @AccountId
            AND t.CategoryId = @CategoryId
            AND t.TypeTransactionId = @ExpenseType
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year)";

        if (_db._connection.State != ConnectionState.Open)
            throw new Exception("connection lost");

        return await _db._connection.ExecuteScalarAsync<decimal>(query, new
        {
            AccountId = accountId,
            CategoryId = categoryId,
            Month = month,
            Year = year,
            ExpenseType = (long)TypeTransactions.EXPENSE
        }, _db._transaction);
    }

    public async Task<IPagedResult<Transactions>> FilterByPaidAsync(long accountId, long month, long year, bool paid, int pageNumber = 1)
    {

        const int pageSize = 10;

        pageNumber = Math.Max(1, pageNumber);
        var offset = (pageNumber - 1) * pageSize;

        var query = @"
        SELECT {TransactionWithContactAndCategoryColumns}
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        WHERE t.AccountId = @AccountId 
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year) AND t.Paid = @Paid
        ORDER BY t.Id DESC
        LIMIT @PageSize OFFSET @OffSet;

        SELECT COUNT(1)
        FROM Transactions t
        INNER JOIN Contact ct ON t.ContactId = ct.Id
        INNER JOIN Category cat ON t.CategoryId = cat.Id
        WHERE t.AccountId = @AccountId 
            AND (EXTRACT(MONTH FROM t.CompetenceDate) = @Month AND EXTRACT(YEAR FROM t.CompetenceDate) = @Year) AND t.Paid = @Paid;";

        if (_db._connection.State != ConnectionState.Open)
        {
            throw new Exception("connection lost");
        }

        using var multi = await _db._connection.QueryMultipleAsync(
            query,
            new { AccountId = accountId, Month = month, Year = year, Paid = paid, OffSet = offset, PageSize = pageSize },
            _db._transaction);

        var items = multi.Read<Transactions, Contact, Category, Transactions>(
            (t, c, cat) =>
            {
                t.Contact = c;
                t.Category = cat;
                return t;
            },
            splitOn: "Id,Id").ToList();

        var totalRecords = (int)await multi.ReadSingleAsync<long>();

        return new IPagedResult<Transactions>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            Items = items
        };
    }
}


