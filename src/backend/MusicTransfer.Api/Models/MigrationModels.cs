namespace MusicTransfer.Api.Models;

public class CreateJobRequest
{
    public List<string> PlaylistIds { get; set; } = new();
}

public class MigrationJob
{
    public Guid Id { get; set; }
    public string SourceProvider { get; set; } = string.Empty;
    public string TargetProvider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<string> PlaylistIds { get; set; } = new();
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
