using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MusicTransfer.Api.Models;

namespace MusicTransfer.Api.Services;

public class MockSpotifyClient : ISpotifyClient
{
    public Task<IReadOnlyCollection<SourceTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
    {
        var seed = Math.Abs(playlistId.GetHashCode());
        var tracks = new List<SourceTrack>();
        for (int i = 0; i < 12; i++)
        {
            tracks.Add(new SourceTrack
            {
                SpotifyTrackId = $"sp_{playlistId}_{i}",
                Title = $"Track {(seed + i) % 50}",
                Artist = $"Artist {(seed + i * 7) % 20}",
                DurationMs = 180_000 + (i * 1000),
                Isrc = i % 3 == 0 ? $"ISRC{(seed + i):000000}" : null
            });
        }

        return Task.FromResult<IReadOnlyCollection<SourceTrack>>(tracks);
    }
}

public class MockYouTubeMusicClient : IYouTubeMusicClient
{
    public Task<IReadOnlyCollection<YouTubeTrackCandidate>> SearchTracksAsync(SourceTrack source, CancellationToken ct = default)
    {
        var candidates = new List<YouTubeTrackCandidate>
        {
            new() { YouTubeTrackId = $"yt_{source.SpotifyTrackId}_a", Title = source.Title, Artist = source.Artist, DurationMs = source.DurationMs, Isrc = source.Isrc },
            new() { YouTubeTrackId = $"yt_{source.SpotifyTrackId}_b", Title = source.Title + " (Live)", Artist = source.Artist, DurationMs = source.DurationMs + 3500 },
            new() { YouTubeTrackId = $"yt_{source.SpotifyTrackId}_c", Title = source.Title, Artist = $"{source.Artist} feat. X", DurationMs = source.DurationMs + 1200 }
        };

        return Task.FromResult<IReadOnlyCollection<YouTubeTrackCandidate>>(candidates);
    }

    public Task<string> CreatePlaylistAsync(string title, CancellationToken ct = default)
        => Task.FromResult($"yt_playlist_{Guid.NewGuid():N}"[..20]);

    public Task AddTracksAsync(string playlistId, IReadOnlyCollection<string> youtubeTrackIds, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class SpotifyClient : ISpotifyClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMigrationStore _store;

    public SpotifyClient(IHttpClientFactory httpFactory, IMigrationStore store)
    {
        _httpFactory = httpFactory;
        _store = store;
    }

    public async Task<IReadOnlyCollection<SourceTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
    {
        var token = _store.GetOAuthToken("demo-user", "spotify")?.AccessToken
            ?? throw new InvalidOperationException("Spotify token not linked.");

        using var http = _httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.spotify.com/v1/playlists/{playlistId}/tracks?limit=100");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var items = doc.RootElement.GetProperty("items");

        var tracks = new List<SourceTrack>();
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("track", out var t) || t.ValueKind == JsonValueKind.Null) continue;

            var artists = t.TryGetProperty("artists", out var a)
                ? string.Join(", ", a.EnumerateArray().Select(x => x.GetProperty("name").GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                : string.Empty;

            string? isrc = null;
            if (t.TryGetProperty("external_ids", out var ext) && ext.TryGetProperty("isrc", out var i))
                isrc = i.GetString();

            tracks.Add(new SourceTrack
            {
                SpotifyTrackId = t.GetProperty("id").GetString() ?? string.Empty,
                Title = t.GetProperty("name").GetString() ?? string.Empty,
                Artist = artists,
                DurationMs = t.GetProperty("duration_ms").GetInt32(),
                Isrc = isrc
            });
        }

        return tracks;
    }
}

public class YouTubeMusicClient : IYouTubeMusicClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMigrationStore _store;

    public YouTubeMusicClient(IHttpClientFactory httpFactory, IMigrationStore store)
    {
        _httpFactory = httpFactory;
        _store = store;
    }

    public async Task<IReadOnlyCollection<YouTubeTrackCandidate>> SearchTracksAsync(SourceTrack source, CancellationToken ct = default)
    {
        var token = _store.GetOAuthToken("demo-user", "google")?.AccessToken
            ?? throw new InvalidOperationException("Google token not linked.");

        using var http = _httpFactory.CreateClient();
        var q = Uri.EscapeDataString($"{source.Title} {source.Artist}");
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&maxResults=5&q={q}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var items = doc.RootElement.GetProperty("items");

        var candidates = new List<YouTubeTrackCandidate>();
        foreach (var item in items.EnumerateArray())
        {
            var videoId = item.GetProperty("id").GetProperty("videoId").GetString() ?? string.Empty;
            var snippet = item.GetProperty("snippet");
            candidates.Add(new YouTubeTrackCandidate
            {
                YouTubeTrackId = videoId,
                Title = snippet.GetProperty("title").GetString() ?? string.Empty,
                Artist = snippet.GetProperty("channelTitle").GetString() ?? string.Empty,
                DurationMs = source.DurationMs // duration omitted from search response; keep source duration for scoring fallback
            });
        }

        return candidates;
    }

    public async Task<string> CreatePlaylistAsync(string title, CancellationToken ct = default)
    {
        var token = _store.GetOAuthToken("demo-user", "google")?.AccessToken
            ?? throw new InvalidOperationException("Google token not linked.");

        using var http = _httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/youtube/v3/playlists?part=snippet,status");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(JsonSerializer.Serialize(new
        {
            snippet = new { title, description = "Migrated by MusicTransfer" },
            status = new { privacyStatus = "private" }
        }), Encoding.UTF8, "application/json");

        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("YouTube playlist id missing");
    }

    public async Task AddTracksAsync(string playlistId, IReadOnlyCollection<string> youtubeTrackIds, CancellationToken ct = default)
    {
        if (youtubeTrackIds.Count == 0) return;

        var token = _store.GetOAuthToken("demo-user", "google")?.AccessToken
            ?? throw new InvalidOperationException("Google token not linked.");

        using var http = _httpFactory.CreateClient();

        foreach (var trackId in youtubeTrackIds)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/youtube/v3/playlistItems?part=snippet");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(JsonSerializer.Serialize(new
            {
                snippet = new
                {
                    playlistId,
                    resourceId = new { kind = "youtube#video", videoId = trackId }
                }
            }), Encoding.UTF8, "application/json");

            using var res = await http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
        }
    }
}
