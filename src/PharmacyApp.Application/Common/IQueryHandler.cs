namespace PharmacyApp.Application.Common;

/// <summary>
/// Contract for all query handlers. Queries are read-only and must not cause
/// observable side effects on domain state.
/// </summary>
public interface IQueryHandler<TQuery, TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
