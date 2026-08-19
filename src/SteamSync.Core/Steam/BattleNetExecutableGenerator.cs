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
                string[] subKeys = new string[]
                {
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net"
                };

                foreach (var subKey in subKeys)
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subKey))
                    {
                        if (key != null)
                        {
                            bnetPath = key.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrEmpty(bnetPath) && Directory.Exists(bnetPath))
                                break;
                        }
                    }
                }
            }
            catch { }
            
            if (string.IsNullOrEmpty(bnetPath) || !Directory.Exists(bnetPath))
            {
                if (Directory.Exists(@"C:\Program Files (x86)\Battle.net"))
                    bnetPath = @"C:\Program Files (x86)\Battle.net";
                else if (Directory.Exists(@"C:\Program Files\Battle.net"))
                    bnetPath = @"C:\Program Files\Battle.net";
            }

            if (!string.IsNullOrEmpty(bnetPath))
            {
                string exePath = Path.Combine(bnetPath, "Battle.net.exe");
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "--game={{gameUid}}",
                        UseShellExecute = true
                    });
                }
            }
        }
    }
}
""";

        var generator = new DummyExecutableGenerator(_logger);
        return generator.GenerateExecutable(gameUid, sourceCode, outputPath, "BattleNet");
    }
}
