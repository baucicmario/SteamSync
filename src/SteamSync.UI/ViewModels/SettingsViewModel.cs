using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamSync.Core.Models;
using System.Text.Json;

namespace SteamSync.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly string _settingsPath;

    [ObservableProperty]
    private string _steamGridDbApiKey = string.Empty;

    [ObservableProperty]
    private bool _detectEpic = true;

    [ObservableProperty]
    private bool _detectGog = true;

    [ObservableProperty]
    private bool _detectUbisoft = true;

    [ObservableProperty]
    private bool _detectEa = true;

    [ObservableProperty]
    private bool _detectBattleNet = true;

    [ObservableProperty]
    private bool _detectRockstar = true;

    [ObservableProperty]
    private ObservableCollection<string> _customScanDirectories = new();

    [ObservableProperty]
    private string _newDirectoryPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel()
    {
        _settingsPath = AppSettings.GetSettingsFilePath();
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var settings = JsonSerializer.Deserialize<AppSettings>(json, options);
                if (settings != null)
                {
                    SteamGridDbApiKey = settings.SteamGridDbApiKey;
                    DetectEpic = settings.DetectEpic;
                    DetectGog = settings.DetectGog;
                    DetectUbisoft = settings.DetectUbisoft;
                    DetectEa = settings.DetectEa;
                    DetectBattleNet = settings.DetectBattleNet;
                    DetectRockstar = settings.DetectRockstar;
                    CustomScanDirectories = new ObservableCollection<string>(settings.CustomScanDirectories);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                SteamGridDbApiKey = SteamGridDbApiKey,
                DetectEpic = DetectEpic,
                DetectGog = DetectGog,
                DetectUbisoft = DetectUbisoft,
                DetectEa = DetectEa,
                DetectBattleNet = DetectBattleNet,
                DetectRockstar = DetectRockstar,
                CustomScanDirectories = CustomScanDirectories.ToList(),
                UsePlayniteWorker = false // Disabled per architecture change
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            
            StatusMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task BrowseDirectoryAsync()
    {
        try
        {
            var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;

            if (parentWindow?.StorageProvider == null)
            {
                StatusMessage = "File browser is not available.";
                return;
            }

            var folders = await parentWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Custom Scan Directory",
                AllowMultiple = true
            });

            if (folders != null && folders.Count > 0)
            {
                int addedCount = 0;
                foreach (var folder in folders)
                {
                    var localPath = folder.TryGetLocalPath();
                    if (!string.IsNullOrWhiteSpace(localPath) && Directory.Exists(localPath))
                    {
                        if (!CustomScanDirectories.Any(d => string.Equals(d, localPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            CustomScanDirectories.Add(localPath);
                            addedCount++;
                        }
                    }
                }

                if (addedCount > 0)
                {
                    StatusMessage = addedCount == 1
                        ? "Scan directory added."
                        : $"{addedCount} scan directories added.";
                }
                else
                {
                    StatusMessage = "Selected directory is already in the list.";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening folder browser: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddDirectory(string? path = null)
    {
        var targetPath = string.IsNullOrWhiteSpace(path) ? NewDirectoryPath : path;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        targetPath = targetPath.Trim();

        if (CustomScanDirectories.Any(d => string.Equals(d, targetPath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "Directory is already in the list.";
            return;
        }

        if (!Directory.Exists(targetPath))
        {
            StatusMessage = $"Directory does not exist: {targetPath}";
            return;
        }

        CustomScanDirectories.Add(targetPath);
        NewDirectoryPath = string.Empty;
        StatusMessage = $"Added directory: {targetPath}";
    }

    [RelayCommand]
    private void RemoveDirectory(string path)
    {
        if (CustomScanDirectories.Contains(path))
        {
            CustomScanDirectories.Remove(path);
        }
    }
}
