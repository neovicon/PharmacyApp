using PharmacyApp.Application.Abstractions;

namespace PharmacyApp.Infrastructure.Services;

/// <summary>
/// Returns platform-appropriate, writable data directories.
///
/// Windows : %LOCALAPPDATA%\PharmacyApp\  (e.g. C:\Users\…\AppData\Local\PharmacyApp)
/// Linux   : $XDG_DATA_HOME/PharmacyApp/  falling back to ~/.local/share/PharmacyApp/
///
/// The XDG base-directory spec is the standard for Linux desktop applications.
/// Avalonia itself follows this convention on Linux.
/// </summary>
public sealed class AppPathProvider : IAppPathProvider
{
    private const string AppFolderName = "PharmacyApp";

    public string DataDirectory { get; }
    public string LogDirectory { get; }

    public AppPathProvider()
    {
        string baseData = ResolveBaseDataDirectory();
        DataDirectory = EnsureDirectory(Path.Combine(baseData, AppFolderName));
        LogDirectory  = EnsureDirectory(Path.Combine(DataDirectory, "Logs"));
    }

    private static string ResolveBaseDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            // LocalApplicationData = %LOCALAPPDATA% — not roamed, local machine only.
            return Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create);
        }

        // Linux / macOS: respect XDG_DATA_HOME; fall back to ~/.local/share
        string? xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdg) && Path.IsPathRooted(xdg))
            return xdg;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share");
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
