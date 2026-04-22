namespace MusicTransfer.Api.Models;

public class CreateJobRequest
{
    public List<string> PlaylistIds { get; set; } = new();
}

public class MigrationJob
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "demo-user";
    public string SourceProvider { get; set; } = string.Empty;
    public string TargetProvider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public List<string> PlaylistIds { get; set; } = new();
}

public class SourceTrack
{
    public string SpotifyTrackId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public string? Isrc { get; set; }
}

public class YouTubeTrackCandidate
{
    public string YouTubeTrackId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public string? Isrc { get; set; }
}

public class TrackMatchResult
{
    public string SpotifyTrackId { get; set; } = string.Empty;
    public string? YouTubeTrackId { get; set; }
    public double Confidence { get; set; }
    public string MatchMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // accepted/review/skipped
    public SourceTrack SourceTrack { get; set; } = new();
    public List<YouTubeTrackCandidate> Candidates { get; set; } = new();
}

public class MigrationReport
{
    public Guid JobId { get; set; }
    public int TotalTracks { get; set; }
    public int Matched { get; set; }
    public int NeedsReview { get; set; }
    public int Skipped { get; set; }
    public int Migrated { get; set; }
    public List<string> TargetPlaylistIds { get; set; } = new();
}

public class ReviewDecision
{
    public string SpotifyTrackId { get; set; } = string.Empty;
    public string? YouTubeTrackId { get; set; }
    public bool Skip { get; set; }
}

public class SubmitReviewRequest
{
    public List<ReviewDecision> Decisions { get; set; } = new();
}

public record OAuthStartResponse(string Provider, string State, string AuthorizationUrl);
public record OAuthCallbackResponse(string Provider, bool Linked, string Message);
public record MigrationRequested(Guid JobId, string UserId, IReadOnlyCollection<string> PlaylistIds);

public class OAuthOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RedirectUri { get; set; }
}
