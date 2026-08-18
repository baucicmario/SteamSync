using System.Net.Http.Headers;
using System.Text.Json;
using SteamSync.Core.Models;

namespace SteamSync.Core.Artwork;

/// <summary>
/// HTTP client for the SteamGridDB API v2.
/// Base URL: https://www.steamgriddb.com/api/v2
/// Auth: Bearer token via Authorization header.
/// </summary>
public class SteamGridDbClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _imageClient;
    private const string BaseUrl = "https://www.steamgriddb.com/api/v2";

    public SteamGridDbClient(string apiKey)
    {
        _httpClient = new HttpClient();
        _imageClient = new HttpClient();
        UpdateApiKey(apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SteamSync/1.0 (https://github.com/baucicmario/SteamSync)");
        _imageClient.DefaultRequestHeaders.Add("User-Agent", "SteamSync/1.0 (https://github.com/baucicmario/SteamSync)");
    }

    public void UpdateApiKey(string apiKey)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>
    /// Searches for a game by title using fuzzy matching.
    /// </summary>
    public async Task<List<SteamGridDbGame>> SearchGamesAsync(string term, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/search/autocomplete/{Uri.EscapeDataString(term)}";
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<SteamGridDbSearchResponse>(json);
        return result?.Success == true ? result.Data : new List<SteamGridDbGame>();
    }

    /// <summary>
    /// Gets grid images (cover art) for a game. Dimensions can be '600x900' for portrait or '460x215,920x430' for landscape.
    /// </summary>
    public Task<List<SteamGridDbImage>> GetGridsAsync(int gameId, string? dimensions = null, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/grids/game/{gameId}?mimes=image/png,image/jpeg&types=static";
        if (!string.IsNullOrEmpty(dimensions))
            url += $"&dimensions={dimensions}";
        return GetImagesAsync(url, ct);
    }

    /// <summary>
    /// Gets hero images (wide banner) for a game.
    /// </summary>
    public Task<List<SteamGridDbImage>> GetHeroesAsync(int gameId, CancellationToken ct = default)
        => GetImagesAsync($"{BaseUrl}/heroes/game/{gameId}?mimes=image/png,image/jpeg&types=static", ct);

    /// <summary>
    /// Gets logo images for a game.
    /// </summary>
    public Task<List<SteamGridDbImage>> GetLogosAsync(int gameId, CancellationToken ct = default)
        => GetImagesAsync($"{BaseUrl}/logos/game/{gameId}?mimes=image/png&types=static", ct);

    /// <summary>
    /// Gets icon images for a game.
    /// </summary>
    public Task<List<SteamGridDbImage>> GetIconsAsync(int gameId, CancellationToken ct = default)
        => GetImagesAsync($"{BaseUrl}/icons/game/{gameId}?mimes=image/png,image/x-icon,image/vnd.microsoft.icon&types=static", ct);

    /// <summary>
    /// Downloads an image from a URL and returns its bytes.
    /// </summary>
    public async Task<byte[]> DownloadImageAsync(string imageUrl, CancellationToken ct = default)
    {
        return await _imageClient.GetByteArrayAsync(imageUrl, ct);
    }

    private async Task<List<SteamGridDbImage>> GetImagesAsync(string url, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<SteamGridDbImageResponse>(json);
        return result?.Success == true ? result.Data : new List<SteamGridDbImage>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _imageClient.Dispose();
    }
}
