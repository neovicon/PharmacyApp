using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Settings.Queries;

/// <summary>
/// Query to retrieve current database health and backup history.
/// </summary>
public sealed record GetDatabaseStatusQuery;

/// <summary>
/// Handles retrieving database status via the management service abstraction.
/// </summary>
public sealed class GetDatabaseStatusQueryHandler : IQueryHandler<GetDatabaseStatusQuery, DatabaseStatusDto>
{
    private readonly IDatabaseManagementService _databaseManagementService;

    public GetDatabaseStatusQueryHandler(IDatabaseManagementService databaseManagementService)
    {
        _databaseManagementService = databaseManagementService;
    }

    public async Task<Result<DatabaseStatusDto>> HandleAsync(GetDatabaseStatusQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _databaseManagementService.GetDatabaseStatusAsync(cancellationToken);
            return Result.Success(status);
        }
        catch (Exception ex)
        {
            return Result.Failure<DatabaseStatusDto>($"Failed to inspect database status: {ex.Message}");
        }
    }
}
