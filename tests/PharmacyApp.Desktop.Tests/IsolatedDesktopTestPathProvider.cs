using PharmacyApp.Application.Abstractions;

namespace PharmacyApp.Desktop.Tests;

public sealed class IsolatedDesktopTestPathProvider : IAppPathProvider, IDisposable
{
    public string DataDirectory { get; }
    public string LogDirectory { get; }

    public IsolatedDesktopTestPathProvider()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "PharmacyApp_DesktopTests_" + Guid.NewGuid().ToString("N"));
        LogDirectory = Path.Combine(DataDirectory, "Logs");
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DataDirectory))
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup in temp directory
        }
    }
}
