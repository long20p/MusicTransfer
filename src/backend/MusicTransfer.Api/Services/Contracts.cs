using MusicTransfer.Api.Models;

namespace MusicTransfer.Api.Services;

public interface IMigrationStore
{
    MigrationJob CreateJob(CreateJobRequest request, string userId = "demo-user");
    MigrationJob? GetJob(Guid id);
    void UpdateJobStatus(Guid id, string status);

    void LinkProvider(string userId, string provider, string state);
    Dictionary<string, string> GetLinkedProviders(string userId);
    void SaveOAuthToken(string userId, string provider, OAuthTokenRecord token);
    OAuthTokenRecord? GetOAuthToken(string userId, string provider);

    void SaveSourceTracks(Guid jobId, IEnumerable<SourceTrack> tracks);
    IReadOnlyCollection<SourceTrack> GetSourceTracks(Guid jobId);

    void SaveMatchResults(Guid jobId, IEnumerable<TrackMatchResult> matches);
    IReadOnlyCollection<TrackMatchResult> GetMatchResults(Guid jobId);

    void SaveReport(Guid jobId, MigrationReport report);
    MigrationReport? GetReport(Guid jobId);

    void SaveTargetPlaylistIds(Guid jobId, IEnumerable<string> playlistIds);
    IReadOnlyCollection<string> GetTargetPlaylistIds(Guid jobId);
}

public interface IOAuthStateStore
{
    string CreateState(string provider);
    bool ValidateAndConsume(string state, string provider);
}

public interface IJobQueue
{
    void Enqueue(MigrationRequested message);
    bool TryDequeue(out MigrationRequested? message);
    IReadOnlyCollection<MigrationRequested> PeekAll();
}

public interface ISpotifyClient
{
    Task<IReadOnlyCollection<SourceTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default);
}

public interface IYouTubeMusicClient
{
    Task<IReadOnlyCollection<YouTubeTrackCandidate>> SearchTracksAsync(SourceTrack source, CancellationToken ct = default);
    Task<string> CreatePlaylistAsync(string title, CancellationToken ct = default);
    Task AddTracksAsync(string playlistId, IReadOnlyCollection<string> youtubeTrackIds, CancellationToken ct = default);
}

public interface IMatchingService
{
    TrackMatchResult Match(SourceTrack source, IReadOnlyCollection<YouTubeTrackCandidate> candidates);
}

public interface IMigrationEngine
{
    Task<MigrationReport> RunAsync(Guid jobId, CancellationToken ct = default);
    Task<MigrationReport> ApplyReviewAndFinalizeAsync(Guid jobId, IReadOnlyCollection<ReviewDecision> decisions, CancellationToken ct = default);
}
