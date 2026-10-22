using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Settings.Commands;

/// <summary>
/// Command to create a verified, WAL-safe SQLite database backup.
/// </summary>
public sealed record CreateManualBackupCommand(Guid CurrentUserId);

/// <summary>
/// Handles manual database backup requests with administrative authorization.
/// </summary>
public sealed class CreateManualBackupCommandHandler : ICommandHandler<CreateManualBackupCommand, BackupInfoDto>
{
    private readonly IDatabaseManagementService _databaseManagementService;
    private readonly IUserRepository _userRepository;

    public CreateManualBackupCommandHandler(
        IDatabaseManagementService databaseManagementService,
        IUserRepository userRepository)
    {
        _databaseManagementService = databaseManagementService;
        _userRepository = userRepository;
    }

    public async Task<Result<BackupInfoDto>> HandleAsync(CreateManualBackupCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var currentUser = await _userRepository.GetByIdAsync(command.CurrentUserId, cancellationToken);
        if (currentUser is null || !currentUser.IsActive || currentUser.Role != UserRole.Admin)
        {
            return Result.Failure<BackupInfoDto>("Only active Administrators are authorized to trigger database backups.");
        }

        try
        {
            var backupInfo = await _databaseManagementService.CreateBackupAsync(cancellationToken);
            return Result.Success(backupInfo);
        }
        catch (Exception ex)
        {
            return Result.Failure<BackupInfoDto>($"Backup creation failed: {ex.Message}");
        }
    }
}
