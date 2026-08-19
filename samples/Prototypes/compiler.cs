using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

class Program {
    static void Main() {
        string sourceCode = @\"
using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;

namespace BattleNetLauncher {
    class Program {
        static void Main(string[] args) {
            File.WriteAllText(\"\"test.log\"\", \"\"Starting...\"\" + Environment.NewLine);
        }
    }
}
\";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var references = new[] {
            MetadataReference.CreateFromFile(@\"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll\"),
            MetadataReference.CreateFromFile(@\"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll\")
        };
        var compilation = CSharpCompilation.Create(\"test.exe\", new[] { syntaxTree }, references, new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        using (var ms = new MemoryStream()) {
            var result = compilation.Emit(ms);
            Console.WriteLine(\"Success: \" + result.Success);
            if (result.Success) {
                File.WriteAllBytes(\"test.exe\", ms.ToArray());
            } else {
                foreach (var diag in result.Diagnostics) Console.WriteLine(diag);
            }
        }
    }
}
