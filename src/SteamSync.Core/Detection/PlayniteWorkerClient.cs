using System.Diagnostics;
using System.Text.Json;
using SteamSync.Core.Models;

namespace SteamSync.Core.Detection;

/// <summary>
/// Launches the SteamSync.PlayniteWorker.exe (.NET Framework 4.6.2) as an out-of-process
/// CLI worker, reads its JSON stdout output, and deserializes it into DetectedGame models.
///
/// Implements the "JSON CLI Worker" pattern:
/// - Worker is a disposable, headless CLI tool
/// - Accepts --launcher argument
/// - Prints JSON array to stdout
/// - Host enforces strict timeout + zombie process cleanup
/// </summary>
public class PlayniteWorkerClient : IGameDetector
{
    public string Name => "Playnite Worker";
    public string PlatformId => "Playnite";

    private readonly string _workerExePath;
    private readonly int _timeoutSeconds;
    private readonly string? _launcher;
    private readonly SteamSync.Core.Logging.SyncLogger _logger;

    /// <summary>
    /// Creates a PlayniteWorkerClient targeting a specific launcher.
    /// </summary>
    /// <param name="workerExePath">Full path to SteamSync.PlayniteWorker.exe.</param>
    /// <param name="launcher">Launcher to query (e.g., "epic", "gog", "ubisoft", "ea", "battlenet", or "all").</param>
    /// <param name="timeoutSeconds">Maximum time to wait for the worker process.</param>
    public PlayniteWorkerClient(string workerExePath, string launcher = "all", int timeoutSeconds = 30, SteamSync.Core.Logging.SyncLogger? logger = null)
    {
        _workerExePath = workerExePath;
        _launcher = launcher;
        _timeoutSeconds = timeoutSeconds;
        _logger = logger ?? new SteamSync.Core.Logging.SyncLogger();
    }

    public async Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_workerExePath))
        {
            var errorMsg = $"[PlayniteWorkerClient] Worker not found at: {_workerExePath}";
            Debug.WriteLine(errorMsg);
            throw new Exception(errorMsg);
        }

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _workerExePath,
                Arguments = $"--launcher {_launcher}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            process = new Process { StartInfo = startInfo };
            process.Start();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            string finalJson = string.Empty;

            // Read stdout line-by-line asynchronously
            var stdoutTask = Task.Run(async () =>
            {
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = await process.StandardOutput.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.StartsWith("PAYLOAD:"))
                    {
                        finalJson = line.Substring("PAYLOAD:".Length);
                    }
                    else
                    {
                        // Push standard output logs directly to orchestrator
                        _logger.Log("PlayniteWorker", line);
                    }
                }
            }, timeoutCts.Token);
            
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await Task.WhenAll(stdoutTask, process.WaitForExitAsync(timeoutCts.Token));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout reached, not user cancellation
                Debug.WriteLine($"[PlayniteWorkerClient] Worker timed out after {_timeoutSeconds}s, killing process.");
                return Array.Empty<DetectedGame>();
            }

            var stdout = finalJson;
            var stderr = await stderrTask;

            // Check exit code
            if (process.ExitCode != 0)
            {
                var errorMsg = $"[PlayniteWorkerClient] Worker exited with code {process.ExitCode}. Stderr: {stderr}";
                Debug.WriteLine(errorMsg);
                throw new Exception(errorMsg);
            }

            if (string.IsNullOrWhiteSpace(stdout))
                return Array.Empty<DetectedGame>();

            // Deserialize the JSON response
            var response = JsonSerializer.Deserialize<WorkerResponse>(stdout, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (response?.Success != true)
            {
                var errorMsg = $"[PlayniteWorkerClient] Worker reported failure: {response?.Error}";
                Debug.WriteLine(errorMsg);
                throw new Exception(errorMsg);
            }

            // Map worker games to DetectedGame models
            return response.Games
                .Select(g => new DetectedGame
                {
                    Title = g.Title ?? string.Empty,
                    Platform = g.Platform ?? PlatformId,
                    IsOwned = g.IsOwned,
                    IsInstalled = g.IsInstalled,
                    ExePath = g.ExePath,
                    StartDir = g.StartDir,
                    LaunchArguments = g.LaunchArguments,
                })
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlayniteWorkerClient] Error: {ex.Message}");
            return Array.Empty<DetectedGame>();
        }
        finally
        {
            // CRITICAL: Ensure no zombie processes are left behind
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { /* Best effort cleanup */ }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    // Internal DTOs for deserializing worker JSON output
    private class WorkerResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<WorkerGame> Games { get; set; } = new();
    }

    private class WorkerGame
    {
        public string? Title { get; set; }
        public string? Platform { get; set; }
        public bool IsOwned { get; set; }
        public bool IsInstalled { get; set; }
        public string? ExePath { get; set; }
        public string? StartDir { get; set; }
        public string? LaunchArguments { get; set; }
    }
}
