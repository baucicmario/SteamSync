using System;
using SteamSync.Core.Logging;

namespace SteamSync.Core.Steam;

public class EaExecutableGenerator
{
    private readonly SyncLogger _logger;

    public EaExecutableGenerator(SyncLogger logger)
    {
        _logger = logger;
    }

    public bool GenerateExecutable(string offerId, string outputPath)
    {
        string contentId = Detection.EaContentIdResolver.ResolveContentId(offerId);
        string sourceCode = $$"""
using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using System.Windows.Forms;

namespace EaLauncher
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string offerId = @"{{contentId}}";
            string eaLauncherPath = null;
            bool isProtocolRegistered = false;

            // 1. Check EA Desktop registry install locations
            try
            {
                string[] eaKeys = new string[]
                {
                    @"SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop",
                    @"SOFTWARE\Electronic Arts\EA Desktop"
                };

                foreach (var subKey in eaKeys)
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subKey))
                    {
                        if (key != null)
                        {
                            var launcherPath = key.GetValue("LauncherAppPath") as string;
                            if (!string.IsNullOrEmpty(launcherPath) && File.Exists(launcherPath))
                            {
                                eaLauncherPath = launcherPath;
                                break;
                            }

                            var clientPath = key.GetValue("ClientPath") as string;
                            if (!string.IsNullOrEmpty(clientPath) && File.Exists(clientPath))
                            {
                                eaLauncherPath = clientPath;
                                break;
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Check default file locations if registry was empty
            if (string.IsNullOrEmpty(eaLauncherPath) || !File.Exists(eaLauncherPath))
            {
                string[] defaultPaths = new string[]
                {
                    @"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EALauncher.exe",
                    @"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe",
                    @"C:\Program Files (x86)\Electronic Arts\EA Desktop\EA Desktop\EALauncher.exe",
                    @"C:\Program Files (x86)\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe",
                    @"C:\Program Files (x86)\Origin\Origin.exe"
                };

                foreach (var path in defaultPaths)
                {
                    if (File.Exists(path))
                    {
                        eaLauncherPath = path;
                        break;
                    }
                }
            }

            // 3. Check protocol handlers in ClassesRoot
            try
            {
                string[] protocols = new string[] { "origin2", "origin", "link2ea", "ealink", "ea" };
                foreach (var protocol in protocols)
                {
                    using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(protocol))
                    {
                        if (key != null)
                        {
                            isProtocolRegistered = true;
                            break;
                        }
                    }
                }
            }
            catch { }

            string launchUri = $"origin2://game/launch/?offerIds={offerId}";

            // 4. If neither EA launcher exe nor protocol is found, show warning dialog
            if (string.IsNullOrEmpty(eaLauncherPath) && !isProtocolRegistered)
            {
                try
                {
                    MessageBox.Show(
                        "EA App installation or protocol handler (origin2://) was not found.\n\nPlease ensure the EA App is installed or run the EA 'App Recovery' tool to fix your installation.",
                        "EA App Error - SteamSync",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                catch { }
                return;
            }

            // 5. Execute via launcher exe or fallback to protocol handler
            try
            {
                if (!string.IsNullOrEmpty(eaLauncherPath) && File.Exists(eaLauncherPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = eaLauncherPath,
                        Arguments = $"\"{launchUri}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = launchUri,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = launchUri,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }
    }
}
""";

        var generator = new DummyExecutableGenerator(_logger);
        return generator.GenerateExecutable(offerId, sourceCode, outputPath, "EA");
    }
}
