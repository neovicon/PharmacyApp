using System;
using System.IO;
using PharmacyApp.Application.Abstractions;

namespace PharmacyApp.Infrastructure.Tests;

internal sealed class IsolatedTestPathProvider : IAppPathProvider, IDisposable
{
    public string DataDirectory { get; }
    public string LogDirectory { get; }

    public IsolatedTestPathProvider()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "PharmacyApp_Tests_" + Guid.NewGuid().ToString("N"));
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
