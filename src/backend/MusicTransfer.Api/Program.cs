using MusicTransfer.Api.Models;
using MusicTransfer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("Spotify"));
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("Google"));

builder.Services.AddSingleton<IMigrationStore, InMemoryMigrationStore>();
builder.Services.AddSingleton<IOAuthStateStore, InMemoryOAuthStateStore>();
builder.Services.AddSingleton<IJobQueue, InMemoryJobQueue>();

builder.Services.AddSingleton<ISpotifyClient, MockSpotifyClient>();
builder.Services.AddSingleton<IYouTubeMusicClient, MockYouTubeMusicClient>();
builder.Services.AddSingleton<IMatchingService, MatchingService>();
builder.Services.AddSingleton<IMigrationEngine, MigrationEngine>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "api" }));

app.MapGet("/v1/auth/spotify/start", (IOAuthStateStore stateStore, IConfiguration config) =>
{
    var state = stateStore.CreateState("spotify");
    var clientId = config["Spotify:ClientId"] ?? "SPOTIFY_CLIENT_ID";
    var redirectUri = config["Spotify:RedirectUri"] ?? "http://localhost:8080/v1/auth/spotify/callback";
    var scope = Uri.EscapeDataString("playlist-read-private playlist-read-collaborative");

    var url = $"https://accounts.spotify.com/authorize?response_type=code&client_id={Uri.EscapeDataString(clientId)}&scope={scope}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={state}";
    return Results.Ok(new OAuthStartResponse("spotify", state, url));
});

app.MapGet("/v1/auth/google/start", (IOAuthStateStore stateStore, IConfiguration config) =>
{
    var state = stateStore.CreateState("google");
    var clientId = config["Google:ClientId"] ?? "GOOGLE_CLIENT_ID";
    var redirectUri = config["Google:RedirectUri"] ?? "http://localhost:8080/v1/auth/google/callback";
    var scope = Uri.EscapeDataString("openid email profile https://www.googleapis.com/auth/youtube");

    var url = $"https://accounts.google.com/o/oauth2/v2/auth?response_type=code&client_id={Uri.EscapeDataString(clientId)}&scope={scope}&redirect_uri={Uri.EscapeDataString(redirectUri)}&access_type=offline&prompt=consent&state={state}";
    return Results.Ok(new OAuthStartResponse("google", state, url));
});

app.MapGet("/v1/auth/spotify/callback", (string? code, string? state, IOAuthStateStore stateStore, IMigrationStore store) =>
{
    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        return Results.BadRequest(new { error = "missing code/state" });

    if (!stateStore.ValidateAndConsume(state, "spotify"))
        return Results.BadRequest(new { error = "invalid state" });

    // Milestone-2: token exchange skeleton placeholder
    store.LinkProvider("demo-user", "spotify", "oauth-code-received");
    return Results.Ok(new OAuthCallbackResponse("spotify", true, "Spotify account linked (token exchange to be completed in production setup)."));
});

app.MapGet("/v1/auth/google/callback", (string? code, string? state, IOAuthStateStore stateStore, IMigrationStore store) =>
{
    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        return Results.BadRequest(new { error = "missing code/state" });

    if (!stateStore.ValidateAndConsume(state, "google"))
        return Results.BadRequest(new { error = "invalid state" });

    store.LinkProvider("demo-user", "google", "oauth-code-received");
    return Results.Ok(new OAuthCallbackResponse("google", true, "Google account linked (token exchange to be completed in production setup)."));
});

app.MapGet("/v1/providers/links", (IMigrationStore store) =>
{
    return Results.Ok(store.GetLinkedProviders("demo-user"));
});

app.MapPost("/v1/jobs", (CreateJobRequest request, IMigrationStore store, IJobQueue queue) =>
{
    var job = store.CreateJob(request);
    queue.Enqueue(new MigrationRequested(job.Id, "demo-user", request.PlaylistIds));
    return Results.Created($"/v1/jobs/{job.Id}", job);
});

app.MapGet("/v1/jobs/{id:guid}", (Guid id, IMigrationStore store) =>
{
    var job = store.GetJob(id);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

app.MapPost("/v1/jobs/{id:guid}/run", async (Guid id, IMigrationStore store, IMigrationEngine engine, CancellationToken ct) =>
{
    var job = store.GetJob(id);
    if (job is null) return Results.NotFound();

    var report = await engine.RunAsync(id, ct);
    return Results.Ok(report);
});

app.MapGet("/v1/jobs/{id:guid}/review-items", (Guid id, IMigrationStore store) =>
{
    var items = store.GetMatchResults(id)
        .Where(m => m.Status == "review")
        .Select(m => new
        {
            m.SpotifyTrackId,
            source = m.SourceTrack,
            m.Confidence,
            candidates = m.Candidates.Select(c => new { c.YouTubeTrackId, c.Title, c.Artist, c.DurationMs, c.Isrc })
        })
        .ToList();

    return Results.Ok(items);
});

app.MapPost("/v1/jobs/{id:guid}/review-decisions", async (Guid id, SubmitReviewRequest req, IMigrationEngine engine, CancellationToken ct) =>
{
    var report = await engine.ApplyReviewAndFinalizeAsync(id, req.Decisions, ct);
    return Results.Ok(report);
});

app.MapGet("/v1/jobs/{id:guid}/report", (Guid id, IMigrationStore store) =>
{
    var report = store.GetReport(id);
    return report is null ? Results.NotFound() : Results.Ok(report);
});

app.MapGet("/v1/queue/pending", (IJobQueue queue) => Results.Ok(queue.PeekAll()));

app.Run();
