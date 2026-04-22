using System.Collections.Concurrent;
using System.Text;
using MusicTransfer.Api.Models;
using MusicTransfer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("Spotify"));
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("Google"));
builder.Services.AddSingleton<IMigrationStore, InMemoryMigrationStore>();
builder.Services.AddSingleton<IOAuthStateStore, InMemoryOAuthStateStore>();
builder.Services.AddSingleton<IJobQueue, InMemoryJobQueue>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "api" }));

app.MapGet("/v1/auth/spotify/start", (HttpContext ctx, IOAuthStateStore stateStore, IConfiguration config) =>
{
    var state = stateStore.CreateState("spotify");
    var clientId = config["Spotify:ClientId"] ?? "SPOTIFY_CLIENT_ID";
    var redirectUri = config["Spotify:RedirectUri"] ?? "http://localhost:8080/v1/auth/spotify/callback";
    var scope = Uri.EscapeDataString("playlist-read-private playlist-read-collaborative");

    var url = $"https://accounts.spotify.com/authorize?response_type=code&client_id={Uri.EscapeDataString(clientId)}&scope={scope}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={state}";
    return Results.Ok(new OAuthStartResponse("spotify", state, url));
});

app.MapGet("/v1/auth/google/start", (HttpContext ctx, IOAuthStateStore stateStore, IConfiguration config) =>
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

    // Milestone-1 skeleton: store only provider-link marker, token exchange is next milestone.
    store.LinkProvider("demo-user", "spotify", "oauth-code-received");
    return Results.Ok(new OAuthCallbackResponse("spotify", true, "OAuth callback validated. Token exchange to be added in Milestone 2."));
});

app.MapGet("/v1/auth/google/callback", (string? code, string? state, IOAuthStateStore stateStore, IMigrationStore store) =>
{
    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        return Results.BadRequest(new { error = "missing code/state" });

    if (!stateStore.ValidateAndConsume(state, "google"))
        return Results.BadRequest(new { error = "invalid state" });

    store.LinkProvider("demo-user", "google", "oauth-code-received");
    return Results.Ok(new OAuthCallbackResponse("google", true, "OAuth callback validated. Token exchange to be added in Milestone 2."));
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

app.MapGet("/v1/queue/pending", (IJobQueue queue) => Results.Ok(queue.PeekAll()));

app.Run();

namespace MusicTransfer.Api.Services
{
    public interface IMigrationStore
    {
        MigrationJob CreateJob(CreateJobRequest request);
        MigrationJob? GetJob(Guid id);
        void LinkProvider(string userId, string provider, string state);
        Dictionary<string, string> GetLinkedProviders(string userId);
    }

    public class InMemoryMigrationStore : IMigrationStore
    {
        private readonly Dictionary<Guid, MigrationJob> _jobs = new();
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _providerLinks = new();

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
    }

    public interface IOAuthStateStore
    {
        string CreateState(string provider);
        bool ValidateAndConsume(string state, string provider);
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

    public interface IJobQueue
    {
        void Enqueue(MigrationRequested message);
        IReadOnlyCollection<MigrationRequested> PeekAll();
    }

    public class InMemoryJobQueue : IJobQueue
    {
        private readonly ConcurrentQueue<MigrationRequested> _queue = new();

        public void Enqueue(MigrationRequested message) => _queue.Enqueue(message);

        public IReadOnlyCollection<MigrationRequested> PeekAll() => _queue.ToArray();
    }
}
