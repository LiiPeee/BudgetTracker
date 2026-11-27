using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Infrastructure.Repository;
using System.Data;

namespace BudgetTracker.Infrastructure.Persistence.Repository
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DbSession _session;

        public UnitOfWork(DbSession session)
        {
            _session = session;
        }

        public void BeginTransaction()
        {
            if (_session._transaction != null)
                throw new InvalidOperationException("A transaction is already in progress for this session.");

            _session.Open();
            _session._transaction = _session._connection.BeginTransaction();
        }

        public void Commit()
        {
            try
            {
                _session._transaction.Commit();
            }
            catch (Exception)
            {
                _session._transaction.Rollback();
                _session._transaction.Dispose();

                throw;
            }
            finally
            {
                _session._transaction?.Dispose();
                _session._transaction = null;
            }
        }
      
        public void Rollback()
        {
            if (_session._transaction == null) return;

            try
            {
                _session._transaction.Rollback();
            }
            catch (ObjectDisposedException) { }
            finally
            {
                _session._transaction?.Dispose();
                _session._transaction = null;
            }
        }
        public void Dispose()
        {
            _session.Dispose();
        }
    }
}


