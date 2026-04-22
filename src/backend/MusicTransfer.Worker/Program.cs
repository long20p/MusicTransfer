using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

public record MigrationRequested(Guid JobId, string UserId, IReadOnlyCollection<string> PlaylistIds);

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MusicTransfer worker started.");
        _logger.LogInformation("Milestone-1 queue contract ready: MigrationRequested(JobId,UserId,PlaylistIds)");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Milestone-1 skeleton only: queue binding + handlers come in Milestone-2
            _logger.LogInformation("Worker heartbeat at: {time}", DateTimeOffset.Now);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
