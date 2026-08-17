using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SteamSync.UI.Views;

public enum CloudFailureAction
{
    Retry,
    UseLocal,
    Cancel
}

public partial class CloudFailureDialog : Window
{
    /// <summary>
    /// The user's chosen action after cloud failure.
    /// </summary>
    public CloudFailureAction Result { get; private set; } = CloudFailureAction.Cancel;

    public CloudFailureDialog()
    {
        InitializeComponent();
    }

    public CloudFailureDialog(string errorMessage) : this()
    {
        ErrorMessageText.Text = errorMessage;
    }

    private void OnRetryClicked(object? sender, RoutedEventArgs e)
    {
        Result = CloudFailureAction.Retry;
        Close();
    }

    private void OnFallbackClicked(object? sender, RoutedEventArgs e)
    {
        Result = CloudFailureAction.UseLocal;
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = CloudFailureAction.Cancel;
        Close();
    }
}
