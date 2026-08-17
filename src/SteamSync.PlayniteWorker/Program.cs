using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SteamSync.PlayniteAdapter;

namespace SteamSync.PlayniteWorker
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                var launcher = args.Length > 1 && args[0] == "--launcher" ? args[1].ToLower() : "all";
                Console.WriteLine($"[LOG] PlayniteWorker started. Target launcher: {launcher}");
                
                var games = new List<object>();

                if (launcher == "epic" || launcher == "all")
                {
                    Console.WriteLine("[LOG] Authenticating and scanning Epic Games via Cloud...");
                    var service = new PlayniteDetectionService();
                    var detected = service.DetectGamesAsync().GetAwaiter().GetResult();
                    
                    foreach (var g in detected)
                    {
                        games.Add(new { 
                            Title = g.Name, 
                            Platform = "Epic Games", 
                            IsOwned = true, 
                            IsInstalled = g.IsInstalled,
                            ExePath = g.ExecutablePath 
                        });
                    }
                    Console.WriteLine($"[LOG] Completed Epic Games scan. Found {detected.Count} games.");
                }

                // Final JSON payload - cleanly separated from STDOUT logs
                var response = new { Success = true, Games = games };
                Console.WriteLine("PAYLOAD:" + JsonConvert.SerializeObject(response));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ERROR] " + ex.ToString());
                var errResponse = new { Success = false, Error = ex.Message, Games = new List<object>() };
                Console.WriteLine("PAYLOAD:" + JsonConvert.SerializeObject(errResponse));
                return 1;
            }
        }
    }
}
