using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        try
        {
            _logger.Log("BattleNetGenerator", $"Generating dummy executable for Battle.net game: {gameUid}");

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

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var fwDir = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319";
            var fw64Dir = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319";
            var targetDir = Directory.Exists(fw64Dir) ? fw64Dir : fwDir;
            
            var references = new[]
            {
                MetadataReference.CreateFromFile(Path.Combine(targetDir, "mscorlib.dll")),
                MetadataReference.CreateFromFile(Path.Combine(targetDir, "System.dll"))
            };

            var compilation = CSharpCompilation.Create(
                $"BattleNetLauncher_{gameUid}.exe",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.WindowsApplication));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (result.Success)
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllBytes(outputPath, ms.ToArray());
                _logger.Log("BattleNetGenerator", $"Successfully generated executable at: {outputPath}");
                return true;
            }
            else
            {
                var failures = result.Diagnostics.Where(diagnostic => 
                    diagnostic.IsWarningAsError || 
                    diagnostic.Severity == DiagnosticSeverity.Error);
                    
                foreach (var diagnostic in failures)
                {
                    _logger.LogError("BattleNetGenerator", $"Compilation error: {diagnostic.Id}: {diagnostic.GetMessage()}");
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("BattleNetGenerator", $"Failed to generate executable for {gameUid}", ex);
            return false;
        }
    }
}
