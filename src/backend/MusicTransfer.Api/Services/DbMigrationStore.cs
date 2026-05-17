using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using MusicTransfer.Api.Data;
using MusicTransfer.Api.Models;

namespace MusicTransfer.Api.Services;

public class DbMigrationStore : IMigrationStore
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    private readonly ConcurrentDictionary<Guid, List<SourceTrack>> _sourceTracks = new();
    private readonly ConcurrentDictionary<Guid, List<TrackMatchResult>> _matches = new();
    private readonly ConcurrentDictionary<Guid, MigrationReport> _reports = new();
    private readonly ConcurrentDictionary<Guid, List<string>> _targetPlaylistIds = new();

    public DbMigrationStore(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public MigrationJob CreateJob(CreateJobRequest request, string userId = "demo-user")
    {
        using var db = _dbFactory.CreateDbContext();
        var now = DateTime.UtcNow;

        var user = db.Users.SingleOrDefault(x => x.ExternalId == userId);
        if (user is null)
        {
            user = new UserEntity
            {
                Id = Guid.NewGuid(),
                ExternalId = userId,
                CreatedAtUtc = now
            };
            db.Users.Add(user);
        }

        var jobEntity = new MigrationJobEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SourceProvider = "spotify",
            TargetProvider = "youtube-music",
            Status = "queued",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.MigrationJobs.Add(jobEntity);

        foreach (var playlistId in request.PlaylistIds)
        {
            db.MigrationJobPlaylists.Add(new MigrationJobPlaylistEntity
            {
                Id = Guid.NewGuid(),
                JobId = jobEntity.Id,
                SourcePlaylistId = playlistId,
                Status = "queued",
                CreatedAtUtc = now
            });
        }

        db.SaveChanges();

        return new MigrationJob
        {
            Id = jobEntity.Id,
            UserId = userId,
            SourceProvider = jobEntity.SourceProvider,
            TargetProvider = jobEntity.TargetProvider,
            Status = jobEntity.Status,
            CreatedAtUtc = jobEntity.CreatedAtUtc,
            UpdatedAtUtc = jobEntity.UpdatedAtUtc,
            PlaylistIds = request.PlaylistIds.ToList()
        };
    }

    public MigrationJob? GetJob(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();

        var job = db.MigrationJobs.SingleOrDefault(x => x.Id == id);
        if (job is null)
            return null;

        var user = db.Users.SingleOrDefault(x => x.Id == job.UserId);
        var playlists = db.MigrationJobPlaylists
            .Where(x => x.JobId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.SourcePlaylistId)
            .ToList();

        return new MigrationJob
        {
            Id = job.Id,
            UserId = user?.ExternalId ?? "demo-user",
            SourceProvider = job.SourceProvider,
            TargetProvider = job.TargetProvider,
            Status = job.Status,
            CreatedAtUtc = job.CreatedAtUtc,
            UpdatedAtUtc = job.UpdatedAtUtc,
            PlaylistIds = playlists
        };
    }

    public void UpdateJobStatus(Guid id, string status)
    {
        using var db = _dbFactory.CreateDbContext();

        var job = db.MigrationJobs.SingleOrDefault(x => x.Id == id);
        if (job is null)
            return;

        job.Status = status;
        job.UpdatedAtUtc = DateTime.UtcNow;
        db.SaveChanges();
    }

    public void LinkProvider(string userId, string provider, string state)
    {
        using var db = _dbFactory.CreateDbContext();
        var now = DateTime.UtcNow;

        var user = db.Users.SingleOrDefault(x => x.ExternalId == userId);
        if (user is null)
        {
            user = new UserEntity
            {
                Id = Guid.NewGuid(),
                ExternalId = userId,
                CreatedAtUtc = now
            };
            db.Users.Add(user);
        }

        var link = db.OAuthLinks.SingleOrDefault(x => x.UserId == user.Id && x.Provider == provider);
        if (link is null)
        {
            link = new OAuthLinkEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = provider,
                Scope = state,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.OAuthLinks.Add(link);
        }
        else
        {
            link.Scope = state;
            link.UpdatedAtUtc = now;
        }

        db.SaveChanges();
    }

    public Dictionary<string, string> GetLinkedProviders(string userId)
    {
        using var db = _dbFactory.CreateDbContext();

        var user = db.Users.SingleOrDefault(x => x.ExternalId == userId);
        if (user is null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return db.OAuthLinks
            .Where(x => x.UserId == user.Id)
            .Select(x => x.Provider)
            .Distinct()
            .ToDictionary(x => x, _ => "linked", StringComparer.OrdinalIgnoreCase);
    }

    public void SaveOAuthToken(string userId, string provider, OAuthTokenRecord token)
    {
        using var db = _dbFactory.CreateDbContext();
        var now = DateTime.UtcNow;

        var user = db.Users.SingleOrDefault(x => x.ExternalId == userId);
        if (user is null)
        {
            user = new UserEntity
            {
                Id = Guid.NewGuid(),
                ExternalId = userId,
                CreatedAtUtc = now
            };
            db.Users.Add(user);
        }

        var link = db.OAuthLinks.SingleOrDefault(x => x.UserId == user.Id && x.Provider == provider);
        if (link is null)
        {
            link = new OAuthLinkEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = provider,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.OAuthLinks.Add(link);
        }

        link.AccessToken = token.AccessToken;
        link.RefreshToken = token.RefreshToken;
        link.Scope = token.Scope;
        link.TokenExpiresAtUtc = token.ExpiresAtUtc;
        link.UpdatedAtUtc = now;

        db.SaveChanges();
    }

    public OAuthTokenRecord? GetOAuthToken(string userId, string provider)
    {
        using var db = _dbFactory.CreateDbContext();

        var user = db.Users.SingleOrDefault(x => x.ExternalId == userId);
        if (user is null)
            return null;

        var link = db.OAuthLinks.SingleOrDefault(x => x.UserId == user.Id && x.Provider == provider);
        if (link is null)
            return null;

        return new OAuthTokenRecord
        {
            AccessToken = link.AccessToken ?? string.Empty,
            RefreshToken = link.RefreshToken,
            Scope = link.Scope,
            ExpiresAtUtc = link.TokenExpiresAtUtc
        };
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
