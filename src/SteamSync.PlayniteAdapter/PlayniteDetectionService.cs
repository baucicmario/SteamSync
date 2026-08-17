using SteamSync.PlayniteAdapter.MockPlayniteApi;
using EpicLibrary.Services;
using EpicLibrary;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SteamSync.PlayniteAdapter
{
    public class PlayniteAdapterGame
    {
        public string Name { get; set; }
        public string ExecutablePath { get; set; }
        public string StoreId { get; set; }
        public bool IsInstalled { get; set; }
    }

    public class PlayniteDetectionService
    {
        public async Task<List<PlayniteAdapterGame>> DetectGamesAsync()
        {
            var games = new List<PlayniteAdapterGame>();
            
            try
            {
                var api = new SteamSyncPlayniteAPI();
                // Set tokens path inside AppData
                var tokensPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SteamSync", "EpicTokens.json");
                
                var epicClient = new EpicAccountClient(api, tokensPath);
                
                // If not logged in, show auth UI!
                if (!epicClient.GetIsUserLoggedIn())
                {
                    epicClient.Login();
                }
                
                if (epicClient.GetIsUserLoggedIn())
                {
                    var assets = epicClient.GetAssets();
                    foreach (var asset in assets)
                    {
                        if (asset.namespace_ == "ue" || asset.buildVersion == null)
                            continue; // Skip unreal engine stuff and unbuildable assets
                            
                        // We would need EpicLauncher to get install directory if installed, 
                        // but since we are fetching OWNED games, we just add them
                        games.Add(new PlayniteAdapterGame 
                        {
                            Name = asset.title,
                            ExecutablePath = "", // Cloud games don't necessarily have an exe path if uninstalled
                            StoreId = asset.catalogItemId,
                            IsInstalled = false 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting games from Playnite Adapter: {ex.Message}");
            }
            
            return games;
        }
    }
}
