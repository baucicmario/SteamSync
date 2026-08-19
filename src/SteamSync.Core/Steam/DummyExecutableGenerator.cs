using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SteamSync.Core.Logging;

namespace SteamSync.Core.Steam;

public class DummyExecutableGenerator
{
    private readonly SyncLogger _logger;

    public DummyExecutableGenerator(SyncLogger logger)
    {
        _logger = logger;
    }

    public bool GenerateExecutable(string gameUid, string sourceCode, string outputPath, string platformName)
    {
        try
        {
            _logger.Log($"{platformName}Generator", $"Generating dummy executable for {platformName} game: {gameUid}");

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
                $"{platformName}Launcher_{gameUid}.exe",
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
                _logger.Log($"{platformName}Generator", $"Successfully generated executable at: {outputPath}");
                return true;
            }
            else
            {
                var failures = result.Diagnostics.Where(diagnostic => 
                    diagnostic.IsWarningAsError || 
                    diagnostic.Severity == DiagnosticSeverity.Error);
                    
                foreach (var diagnostic in failures)
                {
                    _logger.LogError($"{platformName}Generator", $"Compilation error: {diagnostic.Id}: {diagnostic.GetMessage()}");
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{platformName}Generator", $"Failed to generate executable for {gameUid}", ex);
            return false;
        }
    }
}
