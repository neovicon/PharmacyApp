using System.Data.Common;

namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Provides atomic multi-step write execution wrapped within a database transaction.
/// </summary>
public interface ITransactionRunner
{
    /// <summary>
    /// Executes an asynchronous operation inside a database transaction, automatically
    /// committing if successful or rolling back on error.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<DbTransaction, Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an asynchronous operation returning a result inside a database transaction,
    /// automatically committing if successful or rolling back on error.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<DbTransaction, Task<T>> action, CancellationToken cancellationToken = default);
}
