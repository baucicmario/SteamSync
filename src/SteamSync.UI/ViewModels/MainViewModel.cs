using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamSync.Core.Artwork;
using SteamSync.Core.Data;
using SteamSync.Core.Detection;
using SteamSync.Core.Logging;
using SteamSync.Core.Steam;
using SteamSync.Core.Models;

namespace SteamSync.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    private readonly GameListViewModel _gameListViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    public MainViewModel()
    {
        // Create the centralized logger with Avalonia UI dispatcher
        SyncLogger? loggerRef = null;
        loggerRef = new SyncLogger(line =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                loggerRef?.LogLines.Add(line);
            });
        });
        var logger = loggerRef;

        // Dependency Injection setup (manual for simplicity in Avalonia)
        var detectionService = new GameDetectionService(logger);
        var injectorService = new SteamInjectorService(logger);
        
        var db = new SteamSyncDbContext();
        var gameRepo = new GameRepository(db);
        
        // Read API key for client init
        var settings = ReadSettings();
        var client = new SteamGridDbClient(settings.SteamGridDbApiKey);
        var artworkManager = new ArtworkManager(client, logger);

        _gameListViewModel = new GameListViewModel(detectionService, injectorService, artworkManager, gameRepo, logger);
        _settingsViewModel = new SettingsViewModel();

        // Default view
        _currentPage = _gameListViewModel;
    }

    [RelayCommand]
    private void NavigateToGames()
    {
        CurrentPage = _gameListViewModel;
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentPage = _settingsViewModel;
    }

    private AppSettings ReadSettings()
    {
        var path = AppSettings.GetSettingsFilePath();
        if (System.IO.File.Exists(path))
        {
            try
            {
                var json = System.IO.File.ReadAllText(path);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
            }
            catch { return new AppSettings(); }
        }
        return new AppSettings();
    }
}
