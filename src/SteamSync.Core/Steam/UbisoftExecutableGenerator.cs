using System;
using SteamSync.Core.Logging;

namespace SteamSync.Core.Steam;

public class UbisoftExecutableGenerator
{
    private readonly SyncLogger _logger;

    public UbisoftExecutableGenerator(SyncLogger logger)
    {
        _logger = logger;
    }

    public bool GenerateExecutable(string gameId, string outputPath)
    {
        string sourceCode = $$"""
using System;
using System.Diagnostics;

namespace UbisoftLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            string gameId = @"{{gameId}}";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"uplay://install/{gameId}",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
""";

        var generator = new DummyExecutableGenerator(_logger);
        return generator.GenerateExecutable(gameId, sourceCode, outputPath, "Ubisoft");
    }
}
