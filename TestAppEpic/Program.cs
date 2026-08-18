using System;
using System.IO;
using System.Text.RegularExpressions;
using SteamSync.Core.Logging;
using SteamSync.Core.Steam;
using SteamSync.Core.Utilities;

class Program
{
    static void Main()
    {
        var titles = new[] { "Hogwarts Legacy", "Maneater", "World War Z", "Axiom Verge", "Close to the Sun" };
        var logger = new SyncLogger();
        var generator = new EpicExecutableGenerator(logger);

        foreach (var title in titles)
        {
            var sanitized = TitleSanitizer.Sanitize(title);
            var slug = Regex.Replace(sanitized.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            var outPath = Path.Combine(AppContext.BaseDirectory, "test_out", $"{slug}.exe");
            
            bool success = generator.GenerateExecutable(slug, outPath);
            Console.WriteLine($"Generated for '{title}' (slug: '{slug}') -> {success} at {outPath}");
        }
    }
}
