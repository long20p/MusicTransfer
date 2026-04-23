using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.AddHttpClient();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

public record MigrationRequested(Guid JobId, string UserId, IReadOnlyCollection<string> PlaylistIds);

public class WorkerOptions
{
    public string ApiBaseUrl { get; set; } = "http://localhost:8080";
    public int PollIntervalSeconds { get; set; } = 5;
}

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public Worker(ILogger<Worker> logger, IConfiguration config, IHttpClientFactory httpFactory)
    {
        _logger = logger;
        _config = config;
        _httpFactory = httpFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MusicTransfer worker started.");

        var redisConn = _config.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConn))
        {
            _logger.LogWarning("Redis connection string is missing. Worker cannot consume migration queue.");
            return;
        }

        var apiBaseUrl = _config["Worker:ApiBaseUrl"] ?? "http://localhost:8080";
        var pollSeconds = int.TryParse(_config["Worker:PollIntervalSeconds"], out var parsed) ? parsed : 5;

        using var redis = await ConnectionMultiplexer.ConnectAsync(redisConn);
        var db = redis.GetDatabase();
        const string queueKey = "musictransfer:migration:requested";

        using var http = _httpFactory.CreateClient();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var payload = await db.ListLeftPopAsync(queueKey);
                if (payload.IsNullOrEmpty)
                {
                    await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
                    continue;
                }

                var msg = JsonSerializer.Deserialize<MigrationRequested>(payload!)
                          ?? throw new InvalidOperationException("Queue payload deserialization failed");

                _logger.LogInformation("Dequeued migration job {JobId} for user {UserId}", msg.JobId, msg.UserId);

                var res = await http.PostAsync($"{apiBaseUrl.TrimEnd('/')}/v1/jobs/{msg.JobId}/run", content: null, stoppingToken);
                var body = await res.Content.ReadAsStringAsync(stoppingToken);

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogError("Job {JobId} failed via API call. Status={Status}, Body={Body}", msg.JobId, (int)res.StatusCode, body);
                    continue;
                }

                _logger.LogInformation("Job {JobId} processed successfully.", msg.JobId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop error.");
                await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
            }
        }
    }
}
