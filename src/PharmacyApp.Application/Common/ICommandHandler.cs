namespace PharmacyApp.Application.Common;

/// <summary>
/// Contract for all command handlers that return a plain success/failure result.
/// Commands represent mutations with side effects.
/// </summary>
public interface ICommandHandler<TCommand>
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contract for command handlers that return a typed value on success
/// (e.g. the ID of a newly created entity).
/// </summary>
public interface ICommandHandler<TCommand, TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
