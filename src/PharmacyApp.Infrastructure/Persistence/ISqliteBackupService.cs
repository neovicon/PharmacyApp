using System.Threading;
using System.Threading.Tasks;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// Service responsible for creating and verifying WAL-safe SQLite backups.
/// </summary>
public interface ISqliteBackupService
{
    /// <summary>
    /// Creates a consistent, verified SQLite backup file from the source database using VACUUM INTO.
    /// Performs disk space checks, executes VACUUM INTO, and verifies database integrity,
    /// table structure, row counts, and complete user authentication records.
    /// </summary>
    /// <param name="sourceConnectionString">Connection string to the source database.</param>
    /// <param name="targetBackupPath">Target file path where the verified backup will be saved.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verified backup file path.</returns>
    Task<string> CreateBackupAsync(
        string sourceConnectionString,
        string targetBackupPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the integrity of a backup database against its source database.
    /// Checks SQLite PRAGMA integrity_check, table preservation, row count equality,
    /// and row-by-row authentication record preservation.
    /// </summary>
    Task VerifyBackupAsync(
        string sourceConnectionString,
        string backupPath,
        CancellationToken cancellationToken = default);
}
