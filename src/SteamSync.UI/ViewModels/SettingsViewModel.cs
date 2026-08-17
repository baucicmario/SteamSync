using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamSync.Core.Models;
using System.Text.Json;
using System;
using System.Linq;
using System.IO;

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
    private ObservableCollection<string> _customScanDirectories = new();

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
    private void AddDirectory(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !CustomScanDirectories.Contains(path) && Directory.Exists(path))
        {
            CustomScanDirectories.Add(path);
        }
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
