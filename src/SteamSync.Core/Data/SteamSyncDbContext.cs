using Microsoft.Data.Sqlite;
using SteamSync.Core.Models;

namespace SteamSync.Core.Data;

/// <summary>
/// Lightweight SQLite database context using Microsoft.Data.Sqlite directly.
/// Stores game tracking data at %AppData%\SteamSync\steamsync.db.
/// </summary>
public class SteamSyncDbContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public SteamSyncDbContext(string? dbPath = null)
    {
        dbPath ??= AppSettings.GetDatabaseFilePath();
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Games (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Platform TEXT NOT NULL,
                IsOwned INTEGER NOT NULL DEFAULT 0,
                IsInstalled INTEGER NOT NULL DEFAULT 0,
                ExePath TEXT,
                StartDir TEXT,
                LaunchArguments TEXT,
                SteamAppId INTEGER NOT NULL DEFAULT 0,
                SteamGridDbId INTEGER,
                ArtworkCached INTEGER NOT NULL DEFAULT 0,
                IconPath TEXT,
                LastSynced TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                IsVR INTEGER NOT NULL DEFAULT 0,
                OfficialSteamAppId INTEGER
            );

            CREATE INDEX IF NOT EXISTS IX_Games_Title ON Games(Title);
            CREATE INDEX IF NOT EXISTS IX_Games_Platform ON Games(Platform);
            CREATE INDEX IF NOT EXISTS IX_Games_SteamAppId ON Games(SteamAppId);
        ";
        cmd.ExecuteNonQuery();

        try
        {
            cmd.CommandText = "ALTER TABLE Games ADD COLUMN IsVR INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Column already exists
        }

        try
        {
            cmd.CommandText = "ALTER TABLE Games ADD COLUMN OfficialSteamAppId INTEGER;";
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Column already exists
        }
    }

    public SqliteConnection Connection => _connection;

    public void Dispose()
    {
        _connection.Dispose();
    }
}
