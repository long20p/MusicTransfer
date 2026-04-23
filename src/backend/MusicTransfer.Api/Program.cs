using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MusicTransfer.Api.Data;
using MusicTransfer.Api.Models;
using MusicTransfer.Api.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("Spotify"));
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("Google"));

builder.Services.AddSingleton<IMigrationStore, InMemoryMigrationStore>();
builder.Services.AddSingleton<IOAuthStateStore, InMemoryOAuthStateStore>();

var postgres = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrWhiteSpace(postgres))
{
    builder.Services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(postgres));
}

var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConn))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
    builder.Services.AddSingleton<IJobQueue, RedisJobQueue>();
}
else
{
    builder.Services.AddSingleton<IJobQueue, InMemoryJobQueue>();
}

builder.Services.AddHttpClient();

var useMockProviders = builder.Configuration.GetValue<bool>("Providers:UseMockProviders");
if (useMockProviders)
{
    builder.Services.AddSingleton<ISpotifyClient, MockSpotifyClient>();
    builder.Services.AddSingleton<IYouTubeMusicClient, MockYouTubeMusicClient>();
}
else
{
    builder.Services.AddSingleton<ISpotifyClient, SpotifyClient>();
    builder.Services.AddSingleton<IYouTubeMusicClient, YouTubeMusicClient>();
}

builder.Services.AddSingleton<IMatchingService, MatchingService>();
builder.Services.AddSingleton<IMigrationEngine, MigrationEngine>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "api" }));

app.MapPost("/v1/db/ensure", async (IServiceProvider sp) =>
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetService<AppDbContext>();
    if (db is null) return Results.BadRequest(new { error = "Postgres connection not configured" });

    await db.Database.EnsureCreatedAsync();
    return Results.Ok(new { ensured = true });
});

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

app.MapGet("/v1/auth/spotify/callback", async (string? code, string? state, IOAuthStateStore stateStore, IMigrationStore store, IConfiguration config, IHttpClientFactory httpFactory, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        return Results.BadRequest(new { error = "missing code/state" });

    if (!stateStore.ValidateAndConsume(state, "spotify"))
        return Results.BadRequest(new { error = "invalid state" });

    var clientId = config["Spotify:ClientId"];
    var clientSecret = config["Spotify:ClientSecret"];
    var redirectUri = config["Spotify:RedirectUri"] ?? "http://localhost:8080/v1/auth/spotify/callback";

    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        return Results.BadRequest(new { error = "spotify client credentials are not configured" });

    using var http = httpFactory.CreateClient();
    using var req = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
    req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));
    req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"] = "authorization_code",
        ["code"] = code,
        ["redirect_uri"] = redirectUri
    });

    using var res = await http.SendAsync(req, ct);
    var raw = await res.Content.ReadAsStringAsync(ct);
    if (!res.IsSuccessStatusCode)
        return Results.BadRequest(new { error = "spotify token exchange failed", detail = raw });

    using var doc = JsonDocument.Parse(raw);
    var token = new OAuthTokenRecord
    {
        AccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty,
        RefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
        Scope = doc.RootElement.TryGetProperty("scope", out var s) ? s.GetString() : null,
        ExpiresAtUtc = DateTime.UtcNow.AddSeconds(doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600)
    };

    store.SaveOAuthToken("demo-user", "spotify", token);
    store.LinkProvider("demo-user", "spotify", "linked");
    return Results.Ok(new OAuthCallbackResponse("spotify", true, "Spotify account linked."));
});

app.MapGet("/v1/auth/google/callback", async (string? code, string? state, IOAuthStateStore stateStore, IMigrationStore store, IConfiguration config, IHttpClientFactory httpFactory, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        return Results.BadRequest(new { error = "missing code/state" });

    if (!stateStore.ValidateAndConsume(state, "google"))
        return Results.BadRequest(new { error = "invalid state" });

    var clientId = config["Google:ClientId"];
    var clientSecret = config["Google:ClientSecret"];
    var redirectUri = config["Google:RedirectUri"] ?? "http://localhost:8080/v1/auth/google/callback";

    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        return Results.BadRequest(new { error = "google client credentials are not configured" });

    using var http = httpFactory.CreateClient();
    using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
    req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["client_id"] = clientId,
        ["client_secret"] = clientSecret,
        ["code"] = code,
        ["grant_type"] = "authorization_code",
        ["redirect_uri"] = redirectUri
    });

    using var res = await http.SendAsync(req, ct);
    var raw = await res.Content.ReadAsStringAsync(ct);
    if (!res.IsSuccessStatusCode)
        return Results.BadRequest(new { error = "google token exchange failed", detail = raw });

    using var doc = JsonDocument.Parse(raw);
    var token = new OAuthTokenRecord
    {
        AccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty,
        RefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
        Scope = doc.RootElement.TryGetProperty("scope", out var s) ? s.GetString() : null,
        ExpiresAtUtc = DateTime.UtcNow.AddSeconds(doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600)
    };

    store.SaveOAuthToken("demo-user", "google", token);
    store.LinkProvider("demo-user", "google", "linked");
    return Results.Ok(new OAuthCallbackResponse("google", true, "Google account linked."));
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

app.MapGet("/v1/jobs/{id:guid}/report/export.json", (Guid id, IMigrationStore store) =>
{
    var report = store.GetReport(id);
    if (report is null) return Results.NotFound();

    return Results.File(
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
        "application/json",
        fileDownloadName: $"migration-report-{id}.json");
});

app.MapGet("/v1/jobs/{id:guid}/report/export.csv", (Guid id, IMigrationStore store) =>
{
    var report = store.GetReport(id);
    if (report is null) return Results.NotFound();

    var csv = string.Join("\n", new[]
    {
        "job_id,total_tracks,matched,needs_review,skipped,migrated,target_playlist_ids",
        $"{report.JobId},{report.TotalTracks},{report.Matched},{report.NeedsReview},{report.Skipped},{report.Migrated},\"{string.Join("|", report.TargetPlaylistIds)}\""
    });

    return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileDownloadName: $"migration-report-{id}.csv");
});

app.MapGet("/v1/queue/pending", (IJobQueue queue) => Results.Ok(queue.PeekAll()));

app.Run();
