using PharmacyApp.Infrastructure.Services;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class AppPathProviderTests
{
    [Fact]
    public void Constructor_CreatesDataAndLogDirectories()
    {
        var provider = new AppPathProvider();

        Assert.False(string.IsNullOrWhiteSpace(provider.DataDirectory));
        Assert.False(string.IsNullOrWhiteSpace(provider.LogDirectory));
        Assert.True(Directory.Exists(provider.DataDirectory));
        Assert.True(Directory.Exists(provider.LogDirectory));
    }

    [Fact]
    public void LogDirectory_IsChildOfDataDirectory()
    {
        var provider = new AppPathProvider();

        string expectedLogDir = Path.Combine(provider.DataDirectory, "Logs");
        Assert.Equal(expectedLogDir, provider.LogDirectory);
    }

    [Fact]
    public void DataDirectory_IsWritable()
    {
        var provider = new AppPathProvider();
        string testFile = Path.Combine(provider.DataDirectory, $"probe_{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(testFile, "probe write test");
            Assert.True(File.Exists(testFile));
            string readContent = File.ReadAllText(testFile);
            Assert.Equal("probe write test", readContent);
        }
        finally
        {
            if (File.Exists(testFile))
            {
                File.Delete(testFile);
            }
        }
    }

    [Fact]
    public void LogDirectory_IsWritable()
    {
        var provider = new AppPathProvider();
        string testFile = Path.Combine(provider.LogDirectory, $"log_probe_{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(testFile, "log probe");
            Assert.True(File.Exists(testFile));
        }
        finally
        {
            if (File.Exists(testFile))
            {
                File.Delete(testFile);
            }
        }
    }
}
