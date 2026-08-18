using System;
using SteamSync.Core.Logging;

namespace SteamSync.Core.Steam;

public class BattleNetExecutableGenerator
{
    private readonly SyncLogger _logger;

    public BattleNetExecutableGenerator(SyncLogger logger)
    {
        _logger = logger;
    }

    public bool GenerateExecutable(string gameUid, string outputPath)
    {
        string sourceCode = $$"""
using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;

namespace BattleNetLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            string bnetPath = null;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net"))
                {
                    if (key != null)
                    {
                        bnetPath = key.GetValue("InstallLocation") as string;
                    }
                }
            }
            catch { }
            
            if (string.IsNullOrEmpty(bnetPath))
            {
                // Fallback attempt
                bnetPath = @"C:\Program Files (x86)\Battle.net";
            }

            string exePath = Path.Combine(bnetPath, "Battle.net.exe");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--exec=\"launch {{gameUid}}\"",
                    UseShellExecute = true
                });
            }
        }
    }
}
""";

        var generator = new DummyExecutableGenerator(_logger);
        return generator.GenerateExecutable(gameUid, sourceCode, outputPath, "BattleNet");
    }
}
