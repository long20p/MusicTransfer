using MusicTransfer.Api.Models;

namespace MusicTransfer.Api.Services;

public class MatchingService : IMatchingService
{
    public TrackMatchResult Match(SourceTrack source, IReadOnlyCollection<YouTubeTrackCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return new TrackMatchResult
            {
                SpotifyTrackId = source.SpotifyTrackId,
                Confidence = 0,
                MatchMethod = "none",
                Status = "skipped",
                SourceTrack = source
            };
        }

        var scored = candidates
            .Select(c => new
            {
                Candidate = c,
                Score = Score(source, c)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var best = scored.First();
        var status = best.Score >= 0.9 ? "accepted" : best.Score >= 0.7 ? "accepted" : "review";

        return new TrackMatchResult
        {
            SpotifyTrackId = source.SpotifyTrackId,
            YouTubeTrackId = status == "accepted" ? best.Candidate.YouTubeTrackId : null,
            Confidence = best.Score,
            MatchMethod = best.Candidate.Isrc is not null && best.Candidate.Isrc == source.Isrc ? "isrc" : "metadata",
            Status = status,
            SourceTrack = source,
            Candidates = candidates.ToList()
        };
    }

    private static double Score(SourceTrack source, YouTubeTrackCandidate candidate)
    {
        if (!string.IsNullOrWhiteSpace(source.Isrc) && source.Isrc == candidate.Isrc)
            return 0.99;

        double score = 0;

        if (Normalize(source.Title) == Normalize(candidate.Title)) score += 0.45;
        else if (Normalize(candidate.Title).Contains(Normalize(source.Title))) score += 0.25;

        if (Normalize(source.Artist) == Normalize(candidate.Artist)) score += 0.35;
        else if (Normalize(candidate.Artist).Contains(Normalize(source.Artist))) score += 0.2;

        var durationDiff = Math.Abs(source.DurationMs - candidate.DurationMs);
        if (durationDiff <= 2000) score += 0.2;
        else if (durationDiff <= 5000) score += 0.1;

        if (Normalize(candidate.Title).Contains("live") || Normalize(candidate.Title).Contains("karaoke"))
            score -= 0.1;

        return Math.Max(0, Math.Min(1, score));
    }

    private static string Normalize(string value)
        => value.Trim().ToLowerInvariant();
}

public class MigrationEngine : IMigrationEngine
{
    private readonly IMigrationStore _store;
    private readonly ISpotifyClient _spotify;
    private readonly IYouTubeMusicClient _youtube;
    private readonly IMatchingService _matching;

    public MigrationEngine(IMigrationStore store, ISpotifyClient spotify, IYouTubeMusicClient youtube, IMatchingService matching)
    {
        _store = store;
        _spotify = spotify;
        _youtube = youtube;
        _matching = matching;
    }

    public async Task<MigrationReport> RunAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = _store.GetJob(jobId) ?? throw new InvalidOperationException("Job not found");
        _store.UpdateJobStatus(jobId, "running");

        var allTracks = new List<SourceTrack>();
        foreach (var playlistId in job.PlaylistIds)
        {
            var tracks = await _spotify.GetPlaylistTracksAsync(playlistId, ct);
            allTracks.AddRange(tracks);
        }

        _store.SaveSourceTracks(jobId, allTracks);

        var matches = new List<TrackMatchResult>();
        foreach (var track in allTracks)
        {
            var candidates = await _youtube.SearchTracksAsync(track, ct);
            matches.Add(_matching.Match(track, candidates));
        }

        _store.SaveMatchResults(jobId, matches);

        var accepted = matches.Where(m => m.Status == "accepted" && !string.IsNullOrWhiteSpace(m.YouTubeTrackId)).ToList();
        var review = matches.Where(m => m.Status == "review").ToList();
        var skipped = matches.Count(m => m.Status == "skipped");

        var playlistIds = new List<string>();
        foreach (var src in job.PlaylistIds)
        {
            var pId = await _youtube.CreatePlaylistAsync($"Migrated {src}", ct);
            playlistIds.Add(pId);
            await _youtube.AddTracksAsync(pId, accepted.Select(a => a.YouTubeTrackId!).ToList(), ct);
        }

        _store.SaveTargetPlaylistIds(jobId, playlistIds);

        var report = new MigrationReport
        {
            JobId = jobId,
            TotalTracks = allTracks.Count,
            Matched = accepted.Count,
            NeedsReview = review.Count,
            Skipped = skipped,
            Migrated = accepted.Count,
            TargetPlaylistIds = playlistIds
        };

        _store.SaveReport(jobId, report);
        _store.UpdateJobStatus(jobId, review.Any() ? "needs_review" : "completed");

        return report;
    }

    public async Task<MigrationReport> ApplyReviewAndFinalizeAsync(Guid jobId, IReadOnlyCollection<ReviewDecision> decisions, CancellationToken ct = default)
    {
        var matches = _store.GetMatchResults(jobId).ToList();
        var decisionMap = decisions.ToDictionary(d => d.SpotifyTrackId, d => d);

        foreach (var m in matches.Where(m => m.Status == "review"))
        {
            if (!decisionMap.TryGetValue(m.SpotifyTrackId, out var d))
                continue;

            if (d.Skip)
            {
                m.Status = "skipped";
                m.YouTubeTrackId = null;
                continue;
            }

            m.Status = "accepted";
            m.YouTubeTrackId = d.YouTubeTrackId;
            m.Confidence = Math.Max(m.Confidence, 0.7);
        }

        _store.SaveMatchResults(jobId, matches);

        var accepted = matches.Where(m => m.Status == "accepted" && !string.IsNullOrWhiteSpace(m.YouTubeTrackId)).ToList();
        var targetPlaylistIds = _store.GetTargetPlaylistIds(jobId).ToList();

        foreach (var p in targetPlaylistIds)
            await _youtube.AddTracksAsync(p, accepted.Select(a => a.YouTubeTrackId!).ToList(), ct);

        var report = new MigrationReport
        {
            JobId = jobId,
            TotalTracks = matches.Count,
            Matched = accepted.Count,
            NeedsReview = matches.Count(m => m.Status == "review"),
            Skipped = matches.Count(m => m.Status == "skipped"),
            Migrated = accepted.Count,
            TargetPlaylistIds = targetPlaylistIds
        };

        _store.SaveReport(jobId, report);
        _store.UpdateJobStatus(jobId, report.NeedsReview == 0 ? "completed" : "needs_review");

        return report;
    }
}
