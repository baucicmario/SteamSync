using Avalonia.Controls;
using Avalonia.Interactivity;
using SteamSync.Core.Models;

namespace SteamSync.UI.Views;

public partial class DetectionModeDialog : Window
{
    /// <summary>
    /// The user's chosen detection mode, or null if cancelled.
    /// </summary>
    public DetectionMode? Result { get; private set; }

    public DetectionModeDialog()
    {
        InitializeComponent();
    }

    private void OnLocalClicked(object? sender, RoutedEventArgs e)
    {
        Result = DetectionMode.Local;
        Close();
    }

    private void OnCloudClicked(object? sender, RoutedEventArgs e)
    {
        Result = DetectionMode.Cloud;
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}
