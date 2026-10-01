using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using Dapper;

namespace BudgetTracker.Infrastructure.Persistence.Repository;

/// <summary>
/// Repository base for account-owned entities. Every row-targeting operation is
/// filtered by <c>AccountId</c>, so a caller cannot read, update or delete a row
/// that belongs to another account even when it knows the primary key.
/// </summary>
public class AccountScopedRepositoryBase<T> : IAccountScopedRepository<T>
    where T : class, IAccountOwned
{
    private const int MaxRows = 1000;

    private static readonly Lazy<string> TableNameLazy = new(() =>
        typeof(T).GetCustomAttribute<TableAttribute>()?.Name ?? typeof(T).Name);

    private static readonly Lazy<PropertyInfo[]> MappablePropertiesLazy = new(() =>
        typeof(T).GetProperties()
            .Where(p => p.Name != "Id")
            .Where(p => !IsComplexType(p.PropertyType))
            .ToArray());

    protected readonly DbSession _db;
    protected readonly string _tableName;

    public AccountScopedRepositoryBase(DbSession session)
    {
        _db = session;
        _tableName = TableNameLazy.Value;
    }

    public async Task<T> AddAsync(T entity)
    {
        var properties = GetProperties(entity);
        var columns = string.Join(", ", properties.Keys);
        var values = string.Join(", ", properties.Keys.Select(k => $"@{k}"));

        var query = $"INSERT INTO {_tableName} ({columns}) VALUES ({values}) RETURNING id";

        EnsureConnectionOpen();

        var id = await _db._connection.ExecuteScalarAsync<long>(query, entity, _db._transaction);
        var selectQuery = $"SELECT * FROM {_tableName} WHERE id = @Id";
        return await _db._connection.QueryFirstOrDefaultAsync<T>(selectQuery, new { Id = id }, _db._transaction)
            ?? throw new InvalidOperationException($"Failed to retrieve inserted record from {_tableName}.");
    }

    public async Task<T?> GetByIdAsync(long id, long accountId)
    {
        var query = $"SELECT * FROM {_tableName} WHERE id = @Id AND AccountId = @AccountId";

        EnsureConnectionOpen();

        return await _db._connection.QuerySingleOrDefaultAsync<T>(
            query, new { Id = id, AccountId = accountId }, _db._transaction);
    }

    public async Task<IEnumerable<T>> GetAllAsync(long accountId)
    {
        // Fetch one extra row to detect truncation: if more than MaxRows rows
        // exist, fail loudly instead of silently returning a partial result.
        var query = $"SELECT * FROM {_tableName} WHERE AccountId = @AccountId LIMIT {MaxRows + 1}";

        EnsureConnectionOpen();

        var result = (await _db._connection.QueryAsync<T>(
            query, new { AccountId = accountId }, _db._transaction)).ToList();

        if (result.Count > MaxRows)
            throw new InvalidOperationException(
                $"GetAllAsync for {_tableName} returned more than {MaxRows} rows; the result would be truncated. Use pagination instead.");

        return result;
    }

    public async Task<bool> UpdateAsync(T entity)
    {
        var properties = GetProperties(entity);
        var setClause = string.Join(", ", properties.Keys.Select(k => $"{k} = @{k}"));

        var query = $"UPDATE {_tableName} SET {setClause} WHERE id = @Id AND AccountId = @AccountId";

        EnsureConnectionOpen();

        var result = await _db._connection.ExecuteAsync(query, entity, _db._transaction);
        return result > 0;
    }

    public async Task<bool> DeleteAsync(long id, long accountId)
    {
        var query = $"DELETE FROM {_tableName} WHERE id = @Id AND AccountId = @AccountId";

        EnsureConnectionOpen();

        var result = await _db._connection.ExecuteAsync(
            query, new { Id = id, AccountId = accountId }, _db._transaction);
        return result > 0;
    }

    protected void EnsureConnectionOpen()
    {
        if (_db._connection.State != ConnectionState.Open)
            throw new InvalidOperationException("Database connection is not open.");
    }

    protected Dictionary<string, object> GetProperties(T entity)
    {
        var result = new Dictionary<string, object>();
        foreach (var property in MappablePropertiesLazy.Value)
        {
            var value = property.GetValue(entity);
            if (value != null)
                result[property.Name] = value;
        }

        return result;
    }

    private static bool IsComplexType(Type type)
    {
        if (type.IsGenericType &&
            (type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
             type.GetGenericTypeDefinition() == typeof(ICollection<>)))
        {
            return true;
        }

        return type.IsClass &&
            type != typeof(string) &&
            type != typeof(DateTime) &&
            type != typeof(decimal);
    }
}
