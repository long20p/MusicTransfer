using MusicTransfer.Api.Models;

namespace MusicTransfer.Api.Services;

public class MockSpotifyClient : ISpotifyClient
{
    public Task<IReadOnlyCollection<SourceTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
    {
        // deterministic mock tracks based on playlist id
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
            new()
            {
                YouTubeTrackId = $"yt_{source.SpotifyTrackId}_a",
                Title = source.Title,
                Artist = source.Artist,
                DurationMs = source.DurationMs,
                Isrc = source.Isrc
            },
            new()
            {
                YouTubeTrackId = $"yt_{source.SpotifyTrackId}_b",
                Title = source.Title + " (Live)",
                Artist = source.Artist,
                DurationMs = source.DurationMs + 3500,
                Isrc = null
            },
            new()
            {
                YouTubeTrackId = $"yt_{source.SpotifyTrackId}_c",
                Title = source.Title,
                Artist = $"{source.Artist} feat. X",
                DurationMs = source.DurationMs + 1200,
                Isrc = null
            }
        };

        return Task.FromResult<IReadOnlyCollection<YouTubeTrackCandidate>>(candidates);
    }

    public Task<string> CreatePlaylistAsync(string title, CancellationToken ct = default)
        => Task.FromResult($"yt_playlist_{Guid.NewGuid():N}"[..20]);

    public Task AddTracksAsync(string playlistId, IReadOnlyCollection<string> youtubeTrackIds, CancellationToken ct = default)
        => Task.CompletedTask;
}
