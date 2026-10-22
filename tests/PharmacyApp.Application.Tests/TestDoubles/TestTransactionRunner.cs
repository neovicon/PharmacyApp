using System.Data.Common;
using PharmacyApp.Application.Abstractions;

namespace PharmacyApp.Application.Tests.TestDoubles;

public class TestTransactionRunner : ITransactionRunner
{
    public Task ExecuteInTransactionAsync(Func<DbTransaction, Task> action, CancellationToken cancellationToken = default)
    {
        return action(null!);
    }

    public Task<T> ExecuteInTransactionAsync<T>(Func<DbTransaction, Task<T>> action, CancellationToken cancellationToken = default)
    {
        return action(null!);
    }
}
