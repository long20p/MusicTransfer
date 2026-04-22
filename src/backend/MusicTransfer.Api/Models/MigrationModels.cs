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
