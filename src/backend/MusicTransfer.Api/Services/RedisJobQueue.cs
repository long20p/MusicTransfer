using System.Text.Json;
using MusicTransfer.Api.Models;
using StackExchange.Redis;

namespace MusicTransfer.Api.Services;

public class RedisJobQueue : IJobQueue
{
    private readonly IConnectionMultiplexer _redis;
    private const string QueueKey = "musictransfer:migration:requested";

    public RedisJobQueue(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public void Enqueue(MigrationRequested message)
    {
        var db = _redis.GetDatabase();
        db.ListRightPush(QueueKey, JsonSerializer.Serialize(message));
    }

    public bool TryDequeue(out MigrationRequested? message)
    {
        var db = _redis.GetDatabase();
        var payload = db.ListLeftPop(QueueKey);
        if (payload.IsNullOrEmpty)
        {
            message = null;
            return false;
        }

        message = JsonSerializer.Deserialize<MigrationRequested>(payload!);
        return message is not null;
    }

    public IReadOnlyCollection<MigrationRequested> PeekAll()
    {
        var db = _redis.GetDatabase();
        var values = db.ListRange(QueueKey, 0, -1);

        return values
            .Select(v => JsonSerializer.Deserialize<MigrationRequested>(v!))
            .Where(v => v is not null)
            .Cast<MigrationRequested>()
            .ToList();
    }
}
