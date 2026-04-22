using MusicTransfer.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IMigrationStore, InMemoryMigrationStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "api" }));

app.MapPost("/v1/jobs", (CreateJobRequest request, IMigrationStore store) =>
{
    var job = store.CreateJob(request);
    return Results.Created($"/v1/jobs/{job.Id}", job);
});

app.MapGet("/v1/jobs/{id:guid}", (Guid id, IMigrationStore store) =>
{
    var job = store.GetJob(id);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

app.Run();

public interface IMigrationStore
{
    MigrationJob CreateJob(CreateJobRequest request);
    MigrationJob? GetJob(Guid id);
}

public class InMemoryMigrationStore : IMigrationStore
{
    private readonly Dictionary<Guid, MigrationJob> _jobs = new();

    public MigrationJob CreateJob(CreateJobRequest request)
    {
        var job = new MigrationJob
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            SourceProvider = "spotify",
            TargetProvider = "youtube-music",
            PlaylistIds = request.PlaylistIds,
            Status = "queued"
        };

        _jobs[job.Id] = job;
        return job;
    }

    public MigrationJob? GetJob(Guid id) => _jobs.GetValueOrDefault(id);
}
