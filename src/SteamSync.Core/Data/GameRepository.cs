using Microsoft.Data.Sqlite;
using SteamSync.Core.Models;

namespace SteamSync.Core.Data;

/// <summary>
/// CRUD operations for the Games table in the SQLite database.
/// </summary>
public class GameRepository
{
    private readonly SteamSyncDbContext _db;

    public GameRepository(SteamSyncDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gets all games from the database.
    /// </summary>
    public List<DetectedGame> GetAll()
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Games ORDER BY Title";
        return ReadGames(cmd);
    }

    /// <summary>
    /// Gets a game by its database ID.
    /// </summary>
    public DetectedGame? GetById(int id)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Games WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);
        return ReadGames(cmd).FirstOrDefault();
    }

    /// <summary>
    /// Gets a game by title and platform.
    /// </summary>
    public DetectedGame? GetByTitleAndPlatform(string title, string platform)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Games WHERE Title = @Title AND Platform = @Platform";
        cmd.Parameters.AddWithValue("@Title", title);
        cmd.Parameters.AddWithValue("@Platform", platform);
        return ReadGames(cmd).FirstOrDefault();
    }

    /// <summary>
    /// Gets games that are owned but not currently installed.
    /// </summary>
    public List<DetectedGame> GetUninstalledOwnedGames()
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Games WHERE IsOwned = 1 AND IsInstalled = 0";
        return ReadGames(cmd);
    }

    /// <summary>
    /// Inserts or updates a game. Uses Title+Platform as the unique key for upsert.
    /// </summary>
    public void Upsert(DetectedGame game)
    {
        var existing = GetByTitleAndPlatform(game.Title, game.Platform);

        if (existing != null)
        {
            game.Id = existing.Id;
            if (game.SteamAppId == 0 && existing.SteamAppId != 0)
                game.SteamAppId = existing.SteamAppId;
            if (!game.ArtworkCached && existing.ArtworkCached)
                game.ArtworkCached = existing.ArtworkCached;
            if (game.SteamGridDbId == null && existing.SteamGridDbId != null)
                game.SteamGridDbId = existing.SteamGridDbId;
            if (game.OfficialSteamAppId == null && existing.OfficialSteamAppId != null)
                game.OfficialSteamAppId = existing.OfficialSteamAppId;
            if (game.LastSynced == null && existing.LastSynced != null)
                game.LastSynced = existing.LastSynced;

            Update(game);
        }
        else
        {
            Insert(game);
        }
    }

    /// <summary>
    /// Bulk upserts a list of games.
    /// </summary>
    public void UpsertMany(IEnumerable<DetectedGame> games)
    {
        using var transaction = _db.Connection.BeginTransaction();
        try
        {
            foreach (var game in games)
                Upsert(game);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Inserts a new game.
    /// </summary>
    public void Insert(DetectedGame game)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Games (Title, Platform, IsOwned, IsInstalled, ExePath, StartDir, LaunchArguments,
                                SteamAppId, SteamGridDbId, ArtworkCached, IconPath, LastSynced, IsVR, OfficialSteamAppId)
            VALUES (@Title, @Platform, @IsOwned, @IsInstalled, @ExePath, @StartDir, @LaunchArguments,
                    @SteamAppId, @SteamGridDbId, @ArtworkCached, @IconPath, @LastSynced, @IsVR, @OfficialSteamAppId);
            SELECT last_insert_rowid();";

        AddParameters(cmd, game);
        game.Id = Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Updates an existing game.
    /// </summary>
    public void Update(DetectedGame game)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Games SET
                Title = @Title, Platform = @Platform, IsOwned = @IsOwned, IsInstalled = @IsInstalled,
                ExePath = @ExePath, StartDir = @StartDir, LaunchArguments = @LaunchArguments,
                SteamAppId = @SteamAppId, SteamGridDbId = @SteamGridDbId, ArtworkCached = @ArtworkCached,
                IconPath = @IconPath, LastSynced = @LastSynced, IsVR = @IsVR, OfficialSteamAppId = @OfficialSteamAppId, UpdatedAt = datetime('now')
            WHERE Id = @Id";

        cmd.Parameters.AddWithValue("@Id", game.Id);
        AddParameters(cmd, game);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes a game by ID.
    /// </summary>
    public void Delete(int id)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Games WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Marks all games of a specific platform as not installed (for re-scan).
    /// </summary>
    public void MarkAllUninstalled(string platform)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE Games SET IsInstalled = 0 WHERE Platform = @Platform";
        cmd.Parameters.AddWithValue("@Platform", platform);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes all games from the database.
    /// </summary>
    public void ClearAll()
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Games";
        cmd.ExecuteNonQuery();
    }

    private static void AddParameters(SqliteCommand cmd, DetectedGame game)
    {
        cmd.Parameters.AddWithValue("@Title", game.Title);
        cmd.Parameters.AddWithValue("@Platform", game.Platform);
        cmd.Parameters.AddWithValue("@IsOwned", game.IsOwned ? 1 : 0);
        cmd.Parameters.AddWithValue("@IsInstalled", game.IsInstalled ? 1 : 0);
        cmd.Parameters.AddWithValue("@ExePath", (object?)game.ExePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StartDir", (object?)game.StartDir ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LaunchArguments", (object?)game.LaunchArguments ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SteamAppId", (long)game.SteamAppId);
        cmd.Parameters.AddWithValue("@SteamGridDbId", (object?)game.SteamGridDbId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ArtworkCached", game.ArtworkCached ? 1 : 0);
        cmd.Parameters.AddWithValue("@IconPath", (object?)game.IconPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastSynced", (object?)game.LastSynced?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsVR", game.IsVR ? 1 : 0);
        cmd.Parameters.AddWithValue("@OfficialSteamAppId", (object?)game.OfficialSteamAppId ?? DBNull.Value);
    }

    private static List<DetectedGame> ReadGames(SqliteCommand cmd)
    {
        var games = new List<DetectedGame>();
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var game = new DetectedGame
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Platform = reader.GetString(reader.GetOrdinal("Platform")),
                IsOwned = reader.GetInt32(reader.GetOrdinal("IsOwned")) != 0,
                IsInstalled = reader.GetInt32(reader.GetOrdinal("IsInstalled")) != 0,
                ExePath = reader.IsDBNull(reader.GetOrdinal("ExePath")) ? null : reader.GetString(reader.GetOrdinal("ExePath")),
                StartDir = reader.IsDBNull(reader.GetOrdinal("StartDir")) ? null : reader.GetString(reader.GetOrdinal("StartDir")),
                LaunchArguments = reader.IsDBNull(reader.GetOrdinal("LaunchArguments")) ? null : reader.GetString(reader.GetOrdinal("LaunchArguments")),
                SteamAppId = (uint)reader.GetInt64(reader.GetOrdinal("SteamAppId")),
                SteamGridDbId = reader.IsDBNull(reader.GetOrdinal("SteamGridDbId")) ? null : reader.GetInt32(reader.GetOrdinal("SteamGridDbId")),
                ArtworkCached = reader.GetInt32(reader.GetOrdinal("ArtworkCached")) != 0,
                IconPath = reader.IsDBNull(reader.GetOrdinal("IconPath")) ? null : reader.GetString(reader.GetOrdinal("IconPath")),
                LastSynced = reader.IsDBNull(reader.GetOrdinal("LastSynced")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastSynced"))),
                IsVR = reader.GetInt32(reader.GetOrdinal("IsVR")) != 0,
                OfficialSteamAppId = reader.IsDBNull(reader.GetOrdinal("OfficialSteamAppId")) ? null : (uint)reader.GetInt64(reader.GetOrdinal("OfficialSteamAppId"))
            };

            // Calculate SteamAppId if missing
            if (game.SteamAppId == 0 && !string.IsNullOrWhiteSpace(game.ExePath))
            {
                game.SteamAppId = Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath, game.Title);
            }

            // Check if artwork is present on disk in Steam's grid folder
            if (!game.ArtworkCached && game.SteamAppId != 0)
            {
                game.ArtworkCached = Artwork.ArtworkManager.IsArtworkCached(game.SteamAppId);
            }

            games.Add(game);
        }

        return games;
    }
}
