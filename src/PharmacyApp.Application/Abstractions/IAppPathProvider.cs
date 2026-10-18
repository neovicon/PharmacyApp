namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Abstracts writable file system locations used by the application.
/// Implementations must return paths that exist and are writable on the
/// current platform before returning them.
///
/// Do not write to the installation directory, source tree, or any
/// hardcoded user-specific path.
/// </summary>
public interface IAppPathProvider
{
    /// <summary>
    /// Root directory for all private application data (credentials store,
    /// database, backups). Writable by the current user only.
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// Directory for structured log files. Separate from business data so
    /// logs can be archived or rotated independently.
    /// </summary>
    string LogDirectory { get; }
}
