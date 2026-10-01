using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using BudgetTracker.Core.Domain.Repository;
using Dapper;

namespace BudgetTracker.Infrastructure.Persistence.Repository;

/// <summary>
/// Repository base for entities that are NOT account-scoped (reference data and
/// aggregate roots such as Account). Rows are addressed by primary key only.
/// </summary>
public class RepositoryBase<T> : IRepositoryBase<T> where T : class
{
    private static readonly Lazy<string> TableNameLazy = new(() =>
        typeof(T).GetCustomAttribute<TableAttribute>()?.Name ?? typeof(T).Name);

    private static readonly Lazy<PropertyInfo[]> MappablePropertiesLazy = new(() =>
        typeof(T).GetProperties()
            .Where(p => p.Name != "Id")
            .Where(p => !IsComplexType(p.PropertyType))
            .ToArray());

    protected readonly DbSession _db;
    protected readonly string _tableName;

    public RepositoryBase(DbSession session)
    {
        _db = session;
        _tableName = TableNameLazy.Value;
    }

    public async Task<T> AddAsync(T entity)
    {
        var properties = GetProperties(entity);
        properties.Remove("CreatedAt");
        var columns = string.Join(", ", properties.Keys.Append("CreatedAt"));
        var values = string.Join(", ", properties.Keys.Select(k => $"@{k}").Append("NOW()"));

        var query = $"INSERT INTO {_tableName} ({columns}) VALUES ({values}) RETURNING id";

        await EnsureConnectionOpenAsync();

        var id = await _db._connection.ExecuteScalarAsync<long>(query, entity, _db._transaction);
        var selectQuery = $"SELECT * FROM {_tableName} WHERE id = @Id";
        return await _db._connection.QueryFirstOrDefaultAsync<T>(selectQuery, new { Id = id }, _db._transaction)
            ?? throw new InvalidOperationException($"Failed to retrieve inserted record from {_tableName}.");
    }

    public async Task<T?> GetByIdAsync(long id)
    {
        var query = $"SELECT * FROM {_tableName} WHERE id = @Id";

        await EnsureConnectionOpenAsync();

        return await _db._connection.QuerySingleOrDefaultAsync<T>(query, new { Id = id }, _db._transaction);
    }

    public async Task<bool> UpdateAsync(T entity)
    {
        var properties = GetProperties(entity);
        var setClause = string.Join(", ", properties.Keys.Select(k => $"{k} = @{k}"));

        var query = $"UPDATE {_tableName} SET {setClause} WHERE id = @Id";

        await EnsureConnectionOpenAsync();

        var result = await _db._connection.ExecuteAsync(query, entity, _db._transaction);
        return result > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var query = $"DELETE FROM {_tableName} WHERE id = @Id";

        await EnsureConnectionOpenAsync();

        var result = await _db._connection.ExecuteAsync(query, new { Id = id }, _db._transaction);
        return result > 0;
    }

    protected async Task EnsureConnectionOpenAsync()
    {
        await _db.OpenAsync();
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
