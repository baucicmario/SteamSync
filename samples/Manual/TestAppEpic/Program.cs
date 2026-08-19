using System;
using System.Threading.Tasks;
using SteamSync.Core.Detection;

class Program
{
    static async Task Main()
    {
        var detector = new EpicGamesDetector();
        var games = await detector.DetectGamesAsync();
        Console.WriteLine($"Total Epic Games detected by detector: {games.Count}");
        
        foreach (var g in games)
        {
            if (g.Title.Contains("Twinmotion") || g.Title.Contains("Reality") || g.Title.Contains("DLC") || g.Title.Contains("Soundtrack") || g.Title.Contains("Art Book"))
            {
                Console.WriteLine($"WARNING: Unexpected non-game/DLC detected: {g.Title}");
            }
        }
        Console.WriteLine("Done checking!");
    }
}
