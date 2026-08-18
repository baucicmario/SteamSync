using System;
using System.Threading.Tasks;
using SteamSync.Core.Models;
using SteamSync.Core.Logging;
using SteamSync.Core.Utilities;

class Program
{
    static async Task Main()
    {
        var logger = new SyncLogger();
        
        Console.WriteLine("Testing Borderlands 2 (Should be false)...");
        var bl2 = new DetectedGame { Title = "Borderlands 2" };
        var isVrBl2 = await VrDetectionUtility.IsVrGameAsync(bl2, logger);
        Console.WriteLine($"Result: {isVrBl2}");

        Console.WriteLine("\nTesting Half-Life: Alyx (Should be true)...");
        var hla = new DetectedGame { Title = "Half-Life: Alyx" };
        var isVrHla = await VrDetectionUtility.IsVrGameAsync(hla, logger);
        Console.WriteLine($"Result: {isVrHla}");

        Console.WriteLine("\nTesting VRChat (Should be true)...");
        var vrchat = new DetectedGame { Title = "VRChat" };
        var isVrVrc = await VrDetectionUtility.IsVrGameAsync(vrchat, logger);
        Console.WriteLine($"Result: {isVrVrc}");
    }
}
