using System;
using SteamSync.Core.Logging;

namespace SteamSync.Core.Steam;

public class EpicExecutableGenerator
{
    private readonly SyncLogger _logger;

    public EpicExecutableGenerator(SyncLogger logger)
    {
        _logger = logger;
    }

    public bool GenerateExecutable(string? storeSlug, string outputPath)
    {
        string url = string.IsNullOrWhiteSpace(storeSlug) 
            ? "com.epicgames.launcher://store/library" 
            : $"com.epicgames.launcher://store/p/{storeSlug}";

        string sourceCode = $$"""
using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace EpicLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            string url = @"{{url}}";
            string exePath = null;
            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"com.epicgames.launcher\shell\open\command"))
                {
                    if (key != null)
                    {
                        string command = key.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(command))
                        {
                            var match = Regex.Match(command, "\"([^\"]+)\"");
                            if (match.Success)
                            {
                                exePath = match.Groups[1].Value;
                            }
                        }
                    }
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo {
                    FileName = exePath,
                    Arguments = url,
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }
    }
}
""";

        var generator = new DummyExecutableGenerator(_logger);
        return generator.GenerateExecutable(storeSlug, sourceCode, outputPath, "Epic");
    }
}
