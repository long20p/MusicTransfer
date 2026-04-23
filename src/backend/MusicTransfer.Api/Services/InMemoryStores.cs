using System.Collections.Concurrent;
using System.Text;
using MusicTransfer.Api.Models;

namespace MusicTransfer.Api.Services;

public class InMemoryMigrationStore : IMigrationStore
{
    private readonly ConcurrentDictionary<Guid, MigrationJob> _jobs = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _providerLinks = new();
    private readonly ConcurrentDictionary<Guid, List<SourceTrack>> _sourceTracks = new();
    private readonly ConcurrentDictionary<Guid, List<TrackMatchResult>> _matches = new();
    private readonly ConcurrentDictionary<Guid, MigrationReport> _reports = new();
    private readonly ConcurrentDictionary<Guid, List<string>> _targetPlaylistIds = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, OAuthTokenRecord>> _oauthTokens = new();

    public MigrationJob CreateJob(CreateJobRequest request, string userId = "demo-user")
    {
        var job = new MigrationJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            SourceProvider = "spotify",
            TargetProvider = "youtube-music",
            PlaylistIds = request.PlaylistIds,
            Status = "queued"
        };

        _jobs[job.Id] = job;
        return job;
    }

    public MigrationJob? GetJob(Guid id) => _jobs.GetValueOrDefault(id);

    public void UpdateJobStatus(Guid id, string status)
    {
        if (_jobs.TryGetValue(id, out var job))
        {
            job.Status = status;
            job.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public void LinkProvider(string userId, string provider, string state)
    {
        var existing = _providerLinks.GetOrAdd(userId, _ => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        existing[provider] = state;
    }

    public Dictionary<string, string> GetLinkedProviders(string userId)
    {
        return _providerLinks.TryGetValue(userId, out var links)
            ? new Dictionary<string, string>(links)
            : new Dictionary<string, string>();
    }

    public void SaveOAuthToken(string userId, string provider, OAuthTokenRecord token)
    {
        var existing = _oauthTokens.GetOrAdd(userId, _ => new Dictionary<string, OAuthTokenRecord>(StringComparer.OrdinalIgnoreCase));
        existing[provider] = token;
    }

    public OAuthTokenRecord? GetOAuthToken(string userId, string provider)
    {
        if (!_oauthTokens.TryGetValue(userId, out var tokens)) return null;
        return tokens.GetValueOrDefault(provider);
    }

    public void SaveSourceTracks(Guid jobId, IEnumerable<SourceTrack> tracks) => _sourceTracks[jobId] = tracks.ToList();
    public IReadOnlyCollection<SourceTrack> GetSourceTracks(Guid jobId) => _sourceTracks.GetValueOrDefault(jobId, new());

    public void SaveMatchResults(Guid jobId, IEnumerable<TrackMatchResult> matches) => _matches[jobId] = matches.ToList();
    public IReadOnlyCollection<TrackMatchResult> GetMatchResults(Guid jobId) => _matches.GetValueOrDefault(jobId, new());

    public void SaveReport(Guid jobId, MigrationReport report) => _reports[jobId] = report;
    public MigrationReport? GetReport(Guid jobId) => _reports.GetValueOrDefault(jobId);

    public void SaveTargetPlaylistIds(Guid jobId, IEnumerable<string> playlistIds) => _targetPlaylistIds[jobId] = playlistIds.ToList();
    public IReadOnlyCollection<string> GetTargetPlaylistIds(Guid jobId) => _targetPlaylistIds.GetValueOrDefault(jobId, new());
}

public class InMemoryOAuthStateStore : IOAuthStateStore
{
    private readonly ConcurrentDictionary<string, string> _states = new();

    public string CreateState(string provider)
    {
        var raw = $"{provider}:{Guid.NewGuid():N}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        _states[state] = provider;
        return state;
    }

    public bool ValidateAndConsume(string state, string provider)
    {
        if (!_states.TryRemove(state, out var actualProvider)) return false;
        return string.Equals(actualProvider, provider, StringComparison.OrdinalIgnoreCase);
    }
}

public class InMemoryJobQueue : IJobQueue
{
    private readonly ConcurrentQueue<MigrationRequested> _queue = new();

    public void Enqueue(MigrationRequested message) => _queue.Enqueue(message);

    public bool TryDequeue(out MigrationRequested? message)
    {
        var ok = _queue.TryDequeue(out var msg);
        message = msg;
        return ok;
    }

    public IReadOnlyCollection<MigrationRequested> PeekAll() => _queue.ToArray();
}
