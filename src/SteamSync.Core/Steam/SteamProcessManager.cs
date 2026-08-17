using System.Diagnostics;
using SteamSync.Core.Logging;

namespace SteamSync.Core.Steam;

/// <summary>
/// Manages the Steam process lifecycle: detection, graceful shutdown, and relaunch.
/// Used during Force Sync operations that require writing to shortcuts.vdf while
/// Steam is not holding a lock on the file.
/// </summary>
public class SteamProcessManager
{
    /// <summary>
    /// Checks if Steam is currently running.
    /// </summary>
    public static bool IsSteamRunning()
    {
        return Process.GetProcessesByName("steam").Length > 0;
    }

    /// <summary>
    /// Gracefully shuts down Steam using the steam://shutdown protocol.
    /// Falls back to process kill if the graceful shutdown times out.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeoutSeconds">Seconds to wait for graceful shutdown.</param>
    /// <returns>True if Steam was shut down successfully.</returns>
    public static async Task<bool> ShutdownSteamAsync(SyncLogger logger, int timeoutSeconds = 15)
    {
        if (!IsSteamRunning())
        {
            logger.Log("Steam", "Steam is not running, no shutdown needed.");
            return true;
        }

        try
        {
            // Try graceful shutdown via steam:// protocol
            logger.Log("Steam", "Sending steam://shutdown for graceful shutdown...");
            var psi = new ProcessStartInfo
            {
                FileName = "steam://shutdown",
                UseShellExecute = true,
            };
            Process.Start(psi);

            // Wait for Steam to close
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500);
                if (!IsSteamRunning())
                {
                    logger.Log("Steam", "Steam shut down gracefully.");
                    return true;
                }
            }

            // Graceful shutdown failed, force kill
            logger.Log("Steam", $"Graceful shutdown timed out after {timeoutSeconds}s, force killing.");
            return ForceKillSteam(logger);
        }
        catch (Exception ex)
        {
            logger.LogError("Steam", "Error during shutdown", ex);
            return ForceKillSteam(logger);
        }
    }

    /// <summary>
    /// Aggressively kills all Steam processes and waits for them to exit.
    /// Inspired by BoilR's restarter.rs: no graceful shutdown, direct kill, tight poll loop.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeoutSeconds">Max seconds to wait for all processes to die.</param>
    /// <returns>True if all Steam processes are confirmed dead.</returns>
    public static async Task<bool> ForceKillAndWaitAsync(SyncLogger logger, int timeoutSeconds = 10)
    {
        if (!IsSteamRunning())
        {
            logger.Log("Steam", "Steam is not running, no kill needed.");
            return true;
        }

        var sw = Stopwatch.StartNew();
        logger.Log("Steam", "Force-killing all Steam processes (BoilR-style)...");

        try
        {
            var steamProcesses = Process.GetProcessesByName("steam");
            logger.Log("Steam", $"Found {steamProcesses.Length} Steam process(es).");

            // Kill all steam processes
            foreach (var process in steamProcesses)
            {
                try
                {
                    logger.Log("Steam", $"Killing PID {process.Id} ({process.ProcessName})...");
                    process.Kill();
                }
                catch (Exception ex)
                {
                    logger.LogError("Steam", $"Error killing PID {process.Id}", ex);
                }
                finally
                {
                    process.Dispose();
                }
            }

            // Tight poll loop: wait until all steam processes are gone (200ms intervals, like BoilR)
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (!IsSteamRunning())
                {
                    logger.Log("Steam", $"All Steam processes terminated in {sw.ElapsedMilliseconds}ms.");
                    return true;
                }

                await Task.Delay(200);

                // Re-kill any lingering processes (BoilR re-sends kill on each iteration)
                foreach (var process in Process.GetProcessesByName("steam"))
                {
                    try { process.Kill(); } catch { }
                    finally { process.Dispose(); }
                }
            }

            var stillRunning = IsSteamRunning();
            if (stillRunning)
            {
                logger.LogError("Steam", $"Steam processes still alive after {timeoutSeconds}s timeout.");
            }
            return !stillRunning;
        }
        catch (Exception ex)
        {
            logger.LogError("Steam", "Unexpected error during force kill", ex);
            return false;
        }
        finally
        {
            sw.Stop();
        }
    }

    /// <summary>
    /// Force-kills all Steam processes (synchronous, no wait loop).
    /// </summary>
    public static bool ForceKillSteam(SyncLogger logger)
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("steam"))
            {
                try
                {
                    logger.Log("Steam", $"Force-killing PID {process.Id}...");
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    logger.LogError("Steam", $"Error killing PID {process.Id}", ex);
                }
                finally
                {
                    process.Dispose();
                }
            }

            return !IsSteamRunning();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Launches Steam.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="steamExePath">Optional explicit path. If null, auto-detects.</param>
    public static bool LaunchSteam(SyncLogger logger, string? steamExePath = null)
    {
        try
        {
            var exePath = steamExePath ?? SteamPathResolver.GetSteamExePath();
            if (exePath == null || !File.Exists(exePath))
            {
                logger.LogError("Steam", "Steam executable not found.");
                return false;
            }

            logger.Log("Steam", $"Launching Steam from: {exePath}");
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
            });

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Steam", "Error launching Steam", ex);
            return false;
        }
    }
}
