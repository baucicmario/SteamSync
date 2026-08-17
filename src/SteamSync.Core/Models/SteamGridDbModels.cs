using System.Text.Json.Serialization;

namespace SteamSync.Core.Models;

/// <summary>
/// DTOs for SteamGridDB API v2 responses.
/// Base URL: https://www.steamgriddb.com/api/v2
/// </summary>

public class SteamGridDbSearchResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<SteamGridDbGame> Data { get; set; } = new();
}

public class SteamGridDbGame
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }
}

public class SteamGridDbImageResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<SteamGridDbImage> Data { get; set; } = new();
}

public class SteamGridDbImage
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("style")]
    public string Style { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("nsfw")]
    public bool Nsfw { get; set; }

    [JsonPropertyName("humor")]
    public bool Humor { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("thumb")]
    public string Thumb { get; set; } = string.Empty;

    [JsonPropertyName("mime")]
    public string Mime { get; set; } = string.Empty;
}
