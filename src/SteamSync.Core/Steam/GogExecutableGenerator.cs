using System;
using SteamSync.Core.Logging;

namespace SteamSync.Core.Steam;

public class GogExecutableGenerator
{
    private readonly SyncLogger _logger;

    public GogExecutableGenerator(SyncLogger logger)
    {
        _logger = logger;
    }

    public bool GenerateExecutable(string gameId, string outputPath)
    {
        string sourceCode = $$"""
using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using System.Threading;

namespace GogLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            string gameId = @"{{gameId}}";
            string gogClientDir = null;

            try
            {
                string[] subKeys = new string[]
                {
                    @"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths",
                    @"SOFTWARE\GOG.com\GalaxyClient\paths"
                };

                foreach (var subKey in subKeys)
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subKey))
                    {
                        if (key != null)
                        {
                            gogClientDir = key.GetValue("client") as string;
                            if (!string.IsNullOrEmpty(gogClientDir) && Directory.Exists(gogClientDir))
                                break;
                        }
                    }
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKey))
                    {
                        if (key != null)
                        {
                            gogClientDir = key.GetValue("client") as string;
                            if (!string.IsNullOrEmpty(gogClientDir) && Directory.Exists(gogClientDir))
                                break;
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(gogClientDir) || !Directory.Exists(gogClientDir))
            {
                if (Directory.Exists(@"C:\Program Files (x86)\GOG Galaxy"))
                    gogClientDir = @"C:\Program Files (x86)\GOG Galaxy";
                else if (Directory.Exists(@"C:\Program Files\GOG Galaxy"))
                    gogClientDir = @"C:\Program Files\GOG Galaxy";
            }

            string exePath = !string.IsNullOrEmpty(gogClientDir) ? Path.Combine(gogClientDir, "GalaxyClient.exe") : null;

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"/command=installGame /gameId={gameId}",
                        UseShellExecute = true
                    });
                }
                catch { }

                try
                {
                    Thread.Sleep(1000);
                }
                catch { }
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"goggalaxy://openGameView/{gameId}",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
""";

        var generator = new DummyExecutableGenerator(_logger);
        return generator.GenerateExecutable(gameId, sourceCode, outputPath, "GOG");
    }
}
