using Npgsql;
using Microsoft.Extensions.Configuration;

using System.Data;


namespace BudgetTracker.Infrastructure.Persistence.Repository
{
    public class DbSession : IDisposable
    {
        public IDbConnection _connection { get; }
        public IDbTransaction? _transaction { get; set; }

        public string _connectionString { get; set; }

        private bool _disposed;
        private bool _opened;

        public DbSession(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("BudgetTracker");
            _connection = new NpgsqlConnection(_connectionString);
        }

        public async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            if (_opened) return;

            await ((NpgsqlConnection)_connection).OpenAsync(cancellationToken);
            _opened = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _transaction?.Dispose();
                    _connection.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
