using System.Data.Common;
using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// Executes operations atomically inside an SQLite transaction with proper busy-timeout and foreign-key configurations.
/// </summary>
public sealed class SqliteTransactionRunner : ITransactionRunner
{
    private readonly string _connectionString;

    public SqliteTransactionRunner(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task ExecuteInTransactionAsync(Func<DbTransaction, Task> action, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionHelper.OpenConnectionAsync(_connectionString, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await action(transaction);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<DbTransaction, Task<T>> action, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionHelper.OpenConnectionAsync(_connectionString, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            T result = await action(transaction);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
