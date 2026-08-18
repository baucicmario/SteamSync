using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamSync.Core.Models;
using SteamSync.Core.Detection;
using SteamSync.Core.Steam;
using SteamSync.Core.Artwork;
using SteamSync.Core.Data;
using SteamSync.Core.Logging;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.IO;
using System.Threading;
using Avalonia.Input.Platform;
using System.Collections.Generic;

namespace SteamSync.UI.ViewModels;

public partial class GameListViewModel : ViewModelBase
{
    private readonly GameDetectionService _detectionService;
    private readonly SteamInjectorService _injectorService;
    private readonly ArtworkManager _artworkManager;
    private readonly GameRepository _gameRepository;
    private readonly SyncLogger _logger;
    private readonly SteamSyncDbContext? _db;

    [ObservableProperty]
    private ObservableCollection<DetectedGame> _games = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private double _syncProgress;

    [ObservableProperty]
    private bool _isLogPanelVisible = true;

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private string _currentSyncPlatform = string.Empty;

    [ObservableProperty]
    private bool _isSyncingPlatformVisible;

    [ObservableProperty]
    private string _currentSortColumn = string.Empty;

    [ObservableProperty]
    private bool _isSortAscending = true;

    public SyncLogger Logger { get; }

    public GameListViewModel()
    {
        // Design-time constructor
        _logger = new SyncLogger();
        Logger = _logger;
        _detectionService = new GameDetectionService(_logger);
        _injectorService = new SteamInjectorService(_logger);
        _db = new SteamSyncDbContext();
        _gameRepository = new GameRepository(_db);
        _artworkManager = new ArtworkManager(new SteamGridDbClient(""), _logger);
    }

    public GameListViewModel(
        GameDetectionService detectionService,
        SteamInjectorService injectorService,
        ArtworkManager artworkManager,
        GameRepository gameRepository,
        SyncLogger logger)
    {
        _detectionService = detectionService;
        _injectorService = injectorService;
        _artworkManager = artworkManager;
        _gameRepository = gameRepository;
        _logger = logger;
        Logger = logger;
        LoadGamesFromDb();
    }

    private void LoadGamesFromDb()
    {
        try
        {
            var dbGames = _gameRepository.GetAll();
            foreach (var g in dbGames)
                g.IsSelected = true; // Default all to selected

            Games = new ObservableCollection<DetectedGame>(dbGames);
            ApplySort();
            StatusMessage = $"Loaded {Games.Count} games from database.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading games: {ex.Message}";
            _logger.LogError("UI", "Error loading games from DB", ex);
        }
    }

    [RelayCommand]
    private async Task DetectGamesAsync()
    {
        // Show the detection mode dialog — no default, user must choose
        var modeDialog = new SteamSync.UI.Views.DetectionModeDialog();
        var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow : null;

        if (parentWindow != null)
            await modeDialog.ShowDialog(parentWindow);
        else
            modeDialog.Show();

        var chosenMode = modeDialog.Result;
        if (chosenMode == null)
        {
            _logger.Log("UI", "Detection cancelled by user.");
            return;
        }

        await RunDetectionAsync(chosenMode.Value);
    }

    private async Task RunDetectionAsync(SteamSync.Core.Models.DetectionMode mode)
    {
        IsBusy = true;
        StatusMessage = mode == SteamSync.Core.Models.DetectionMode.Local
            ? "Detecting games (Local Offline)..."
            : "Detecting games (Cloud / Playnite)...";
        SyncProgress = 0;
        Games.Clear();

        try
        {
            var settings = ReadSettings();

            if (mode == SteamSync.Core.Models.DetectionMode.Local)
                _detectionService.ConfigureDefaults(settings);
            else
                _detectionService.ConfigurePlaynite(settings);

            var progress = new Progress<(string detector, int count)>(p =>
            {
                StatusMessage = $"Scanning {p.detector}... (Found {p.count})";
                CurrentSyncPlatform = p.detector;
                IsSyncingPlatformVisible = true;
            });

            List<DetectedGame> detected;
            try
            {
                detected = await _detectionService.DetectAllGamesAsync(progress);
            }
            catch (Exception ex) when (mode == SteamSync.Core.Models.DetectionMode.Cloud)
            {
                // Cloud detection failed — show fallback dialog
                _logger.LogError("UI", "Cloud detection failed", ex);
                IsBusy = false;

                var failureAction = await ShowCloudFailureDialogAsync(ex.Message);
                switch (failureAction)
                {
                    case SteamSync.UI.Views.CloudFailureAction.Retry:
                        await RunDetectionAsync(SteamSync.Core.Models.DetectionMode.Cloud);
                        return;
                    case SteamSync.UI.Views.CloudFailureAction.UseLocal:
                        await RunDetectionAsync(SteamSync.Core.Models.DetectionMode.Local);
                        return;
                    default: // Cancel
                        StatusMessage = "Detection cancelled.";
                        return;
                }
            }

            SyncProgress = 40;

            if (!string.IsNullOrWhiteSpace(settings.SteamGridDbApiKey))
            {
                StatusMessage = "Resolving exact titles via SteamGridDB...";
                _artworkManager.UpdateApiKey(settings.SteamGridDbApiKey);
                
                // Process in parallel with a limit to avoid rate limits
                var throttler = new SemaphoreSlim(5);
                int resolved = 0;
                int total = detected.Count;

                var resolveTasks = detected.Select(async game => 
                {
                    await throttler.WaitAsync();
                    try 
                    {
                        var resolvedTitle = await _artworkManager.ResolveGameTitleAsync(game.Title);
                        if (!string.IsNullOrWhiteSpace(resolvedTitle))
                        {
                            game.Title = resolvedTitle;
                            if (!string.IsNullOrWhiteSpace(game.ExePath))
                            {
                                game.SteamAppId = AppIdGenerator.GenerateShortcutAppId(game.ExePath, game.Title);
                            }
                            game.ArtworkCached = ArtworkManager.IsArtworkCached(game.SteamAppId);
                        }
                        var current = Interlocked.Increment(ref resolved);
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            SyncProgress = 40 + (current / (double)total) * 40;
                            StatusMessage = $"Resolving titles... ({current}/{total})";
                        });
                    }
                    finally 
                    {
                        throttler.Release();
                    }
                });
                
                await Task.WhenAll(resolveTasks);

                // Re-deduplicate after title resolution (e.g., 'Genshin Impact game' and 'Genshin Impact' resolving to the same title)
                var deduplicated = detected
                    .GroupBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                    {
                        var best = group
                            .OrderByDescending(g => g.IsInstalled ? 1 : 0)
                            .ThenByDescending(g => g.ExePath != null ? 1 : 0)
                            .First();

                        best.IsOwned = group.Any(g => g.IsOwned);
                        best.IsInstalled = group.Any(g => g.IsInstalled);

                        return best;
                    })
                    .OrderBy(g => g.Title)
                    .ToList();
                    
                detected = deduplicated;
            }

            SyncProgress = 80;
            StatusMessage = "Saving to database...";
            _gameRepository.UpsertMany(detected);
            
            SyncProgress = 100;
            LoadGamesFromDb();
            StatusMessage = $"Detection complete. Found {Games.Count} games.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Detection failed: {ex.Message}";
            _logger.LogError("UI", "Detection failed", ex);
        }
        finally
        {
            IsBusy = false;
            SyncProgress = 0;
            IsSyncingPlatformVisible = false;
        }
    }

    private async Task<SteamSync.UI.Views.CloudFailureAction> ShowCloudFailureDialogAsync(string errorMessage)
    {
        var dialog = new SteamSync.UI.Views.CloudFailureDialog(errorMessage);
        var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow : null;

        if (parentWindow != null)
            await dialog.ShowDialog(parentWindow);
        else
            dialog.Show();

        return dialog.Result;
    }

    [RelayCommand]
    private async Task SyncToSteamAsync()
    {
        await PerformSync(forceRestart: false);
    }

    [RelayCommand]
    private async Task ForceSyncAsync()
    {
        await PerformSync(forceRestart: true);
    }

    private async Task PerformSync(bool forceRestart)
    {
        IsBusy = true;
        SyncProgress = 0;
        StatusMessage = "Preparing sync...";

        try
        {
            // Only sync selected games
            var selectedGames = Games.Where(g => g.IsSelected && g.IsInstalled).ToList();
            _logger.Log("UI", $"Syncing {selectedGames.Count} selected game(s) (out of {Games.Count} total)");

            if (selectedGames.Count == 0)
            {
                StatusMessage = "No games selected for sync.";
                _logger.Log("UI", "No games selected, aborting sync.");
                return;
            }

            // Save any manual UI edits (e.g., IsVR toggles) back to the database
            foreach (var game in selectedGames)
            {
                _gameRepository.Update(game);
            }

            var userIds = SteamPathResolver.GetUserIds();
            if (userIds.Count == 0)
            {
                StatusMessage = "Error: No Steam user profiles found.";
                return;
            }

            var settings = ReadSettings();
            var fetchArtwork = !string.IsNullOrWhiteSpace(settings.SteamGridDbApiKey);

            if (fetchArtwork)
            {
                _artworkManager.UpdateApiKey(settings.SteamGridDbApiKey);
                StatusMessage = "Downloading artwork from SteamGridDB...";
                var artworkGames = selectedGames.Where(g => !g.ArtworkCached && !ArtworkManager.IsArtworkCached(g.SteamAppId)).ToList();
                double total = artworkGames.Count;
                double current = 0;

                foreach (var game in artworkGames)
                {
                    foreach (var userId in userIds)
                    {
                        var progress = new Progress<string>(msg => StatusMessage = $"[{game.Title}] {msg}");
                        await _artworkManager.FetchAndSaveArtworkAsync(game, userId, progress);
                    }
                    current++;
                    SyncProgress = (current / total) * 50; // First 50% is artwork
                    
                    if (game.ArtworkCached)
                    {
                        _gameRepository.Update(game);
                    }
                }
            }

            SyncProgress = 60;
            StatusMessage = forceRestart ? "Force syncing Steam shortcuts..." : "Syncing Steam shortcuts...";
            
            SyncResult result;

            if (forceRestart)
                result = await _injectorService.ForceSyncAsync(selectedGames);
            else
                result = await _injectorService.SyncAsync(selectedGames);

            if (result.HasErrors)
            {
                StatusMessage = $"Sync completed with errors: {string.Join(", ", result.Errors)}";
            }
            else
            {
                StatusMessage = $"Sync complete! Added: {result.ShortcutsAdded}, Updated: {result.ShortcutsUpdated}, Removed: {result.ShortcutsRemoved}";
            }
            
            SyncProgress = 100;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed: {ex.Message}";
            _logger.LogError("UI", "Sync failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var game in Games)
            game.IsSelected = true;
        
        if (string.IsNullOrEmpty(CurrentSortColumn))
        {
            var temp = Games;
            Games = new ObservableCollection<DetectedGame>(temp);
        }
        else
        {
            ApplySort();
        }
        
        _logger.Log("UI", $"Selected all {Games.Count} games.");
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var game in Games)
            game.IsSelected = false;
            
        if (string.IsNullOrEmpty(CurrentSortColumn))
        {
            var temp = Games;
            Games = new ObservableCollection<DetectedGame>(temp);
        }
        else
        {
            ApplySort();
        }
        
        _logger.Log("UI", "Deselected all games.");
    }

    [RelayCommand]
    private void Sort(string column)
    {
        if (CurrentSortColumn == column)
        {
            IsSortAscending = !IsSortAscending;
        }
        else
        {
            CurrentSortColumn = column;
            IsSortAscending = true;
        }

        ApplySort();
    }

    private void ApplySort()
    {
        if (Games == null || !Games.Any()) return;

        IEnumerable<DetectedGame> sorted = Games;

        switch (CurrentSortColumn)
        {
            case "Platform":
                sorted = IsSortAscending ? Games.OrderBy(g => g.Platform).ThenBy(g => g.Title) : Games.OrderByDescending(g => g.Platform).ThenBy(g => g.Title);
                break;
            case "Title":
                sorted = IsSortAscending ? Games.OrderBy(g => g.Title) : Games.OrderByDescending(g => g.Title);
                break;
            case "Installed":
                sorted = IsSortAscending ? Games.OrderByDescending(g => g.IsInstalled).ThenBy(g => g.Title) : Games.OrderBy(g => g.IsInstalled).ThenBy(g => g.Title);
                break;
            case "Artwork":
                sorted = IsSortAscending ? Games.OrderByDescending(g => g.ArtworkCached).ThenBy(g => g.Title) : Games.OrderBy(g => g.ArtworkCached).ThenBy(g => g.Title);
                break;
            case "VR":
                sorted = IsSortAscending ? Games.OrderByDescending(g => g.IsVR).ThenBy(g => g.Title) : Games.OrderBy(g => g.IsVR).ThenBy(g => g.Title);
                break;
            case "AppID":
                sorted = IsSortAscending ? Games.OrderBy(g => g.SteamAppId).ThenBy(g => g.Title) : Games.OrderByDescending(g => g.SteamAppId).ThenBy(g => g.Title);
                break;
            case "Selected":
                sorted = IsSortAscending ? Games.OrderByDescending(g => g.IsSelected).ThenBy(g => g.Title) : Games.OrderBy(g => g.IsSelected).ThenBy(g => g.Title);
                break;
        }

        if (!string.IsNullOrEmpty(CurrentSortColumn))
        {
            Games = new ObservableCollection<DetectedGame>(sorted.ToList());
        }
    }

    [RelayCommand]
    private async Task CopyLogsAsync()
    {
        try
        {
            // Avalonia 11: clipboard is accessed via TopLevel
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is 
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;
            if (topLevel != null)
            {
                var clipboard = topLevel.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(_logger.FullLogText);
                    StatusMessage = "Logs copied to clipboard!";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy logs: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _logger.Clear();
    }

    [RelayCommand]
    private void ToggleLogPanel()
    {
        IsLogPanelVisible = !IsLogPanelVisible;
    }

    [RelayCommand]
    private void ClearDatabase()
    {
        try
        {
            _gameRepository.ClearAll();
            Games.Clear();
            StatusMessage = "Database cleared.";
            _logger.Log("UI", "Cleared all games from the database.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error clearing database: {ex.Message}";
            _logger.LogError("UI", "Error clearing database", ex);
        }
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
            catch (Exception ex)
            {
                _logger.Log("Error", $"Failed to read settings: {ex.Message}");
                // Return default settings but log the error
                return new AppSettings();
            }
        }
        return new AppSettings();
    }
}
