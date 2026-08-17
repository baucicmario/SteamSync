using System.Collections.ObjectModel;

namespace SteamSync.Core.Logging;

/// <summary>
/// Centralized, thread-safe logger for SteamSync.
/// Maintains an in-memory log buffer (ObservableCollection for UI binding)
/// and simultaneously writes to a log file on disk.
/// </summary>
public class SyncLogger
{
    private readonly object _lock = new();
    private readonly List<string> _logLines = new();
    private readonly string _logFilePath;
    private readonly Action<string>? _uiDispatcher;

    /// <summary>
    /// Observable log lines for UI binding. Must be updated on the UI thread.
    /// </summary>
    public ObservableCollection<string> LogLines { get; } = new();

    /// <summary>
    /// Full log text for clipboard copy.
    /// </summary>
    public string FullLogText
    {
        get
        {
            lock (_lock)
            {
                return string.Join(Environment.NewLine, _logLines);
            }
        }
    }

    /// <summary>
    /// Creates a new SyncLogger.
    /// </summary>
    /// <param name="uiDispatcher">Optional callback to marshal log additions to the UI thread.</param>
    public SyncLogger(Action<string>? uiDispatcher = null)
    {
        _uiDispatcher = uiDispatcher;

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamSync");
        Directory.CreateDirectory(appData);
        _logFilePath = Path.Combine(appData, "steamsync.log");

        // Truncate old log at session start
        try
        {
            File.WriteAllText(_logFilePath, string.Empty);
        }
        catch { }

        Log("=== SteamSync Session Started ===");
    }

    /// <summary>
    /// Logs a message with timestamp.
    /// </summary>
    public void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";

        lock (_lock)
        {
            _logLines.Add(line);
        }

        // Write to file
        try
        {
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
        catch { }

        // Update observable collection (UI thread)
        if (_uiDispatcher != null)
        {
            _uiDispatcher(line);
        }
        else
        {
            // Fallback: try direct add (works if called from UI thread)
            try { LogLines.Add(line); } catch { }
        }

        System.Diagnostics.Debug.WriteLine(line);
    }

    /// <summary>
    /// Logs a message with a category prefix.
    /// </summary>
    public void Log(string category, string message)
    {
        Log($"[{category}] {message}");
    }

    /// <summary>
    /// Logs an error with exception details.
    /// </summary>
    public void LogError(string category, string message, Exception? ex = null)
    {
        var errorMsg = ex != null ? $"{message}: {ex.Message}" : message;
        Log($"[{category}] ERROR: {errorMsg}");
    }

    /// <summary>
    /// Clears all log lines from memory and the observable collection.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _logLines.Clear();
        }
        LogLines.Clear();
    }
}
