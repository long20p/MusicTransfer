namespace MusicTransfer.Api.Services;

public static class RetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, int maxAttempts = 3, int baseDelayMs = 200, CancellationToken ct = default)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastError = ex;
                var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        throw new InvalidOperationException($"Operation failed after {maxAttempts} attempts", lastError);
    }

    public static async Task ExecuteAsync(Func<Task> action, int maxAttempts = 3, int baseDelayMs = 200, CancellationToken ct = default)
    {
        await ExecuteAsync(async () =>
        {
            await action();
            return true;
        }, maxAttempts, baseDelayMs, ct);
    }
}
