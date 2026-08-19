using System;
using System.IO;
using System.Text.Json;
using System.Linq;

class Program
{
    static void Main()
    {
        var path = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Catalog\catcache.bin";
        if (File.Exists(path))
        {
            var base64 = File.ReadAllText(path);
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            using var doc = JsonDocument.Parse(json);
            foreach (var item in doc.RootElement.EnumerateArray().Take(2))
            {
                Console.WriteLine("Title: " + (item.TryGetProperty("title", out var t) ? t.GetString() : ""));
                Console.WriteLine("ID: " + (item.TryGetProperty("id", out var i) ? i.GetString() : ""));
                Console.WriteLine("Namespace: " + (item.TryGetProperty("namespace", out var n) ? n.GetString() : ""));
                if (item.TryGetProperty("customAttributes", out var attrs))
                {
                    Console.WriteLine("Attributes:");
                    foreach (var attr in attrs.EnumerateObject())
                    {
                        var val = attr.Value.TryGetProperty("value", out var v) ? v.GetString() : "";
                        Console.WriteLine($"  {attr.Name}: {val}");
                    }
                }
                Console.WriteLine("---------------------");
            }
        }
        else
        {
            Console.WriteLine("catcache.bin not found");
        }
    }
}
