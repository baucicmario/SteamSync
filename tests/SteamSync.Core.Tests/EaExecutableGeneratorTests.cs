using System;
using System.IO;
using SteamSync.Core.Logging;
using SteamSync.Core.Steam;

namespace SteamSync.Core.Tests;

public class EaExecutableGeneratorTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly SyncLogger _logger;

    public EaExecutableGeneratorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SteamSyncTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _logger = new SyncLogger();
    }

    [Fact]
    public void GenerateExecutable_CreatesValidExecutable()
    {
        var generator = new EaExecutableGenerator(_logger);
        var outputPath = Path.Combine(_tempDirectory, "Origin.OFR.50.0002694.exe");

        bool result = generator.GenerateExecutable("Origin.OFR.50.0002694", outputPath);

        Assert.True(result);
        Assert.True(File.Exists(outputPath));
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 0);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch { }
    }
}
