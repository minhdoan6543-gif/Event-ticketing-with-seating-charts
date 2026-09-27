using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace TicketBooking.Api.HealthChecks;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _redis;

    public RedisHealthCheck(IConnectionMultiplexer? redis = null)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_redis == null)
        {
            return HealthCheckResult.Unhealthy("Redis connection multiplexer is not registered.");
        }

        if (!_redis.IsConnected)
        {
            return HealthCheckResult.Unhealthy("Redis connection is currently disconnected.");
        }

        try
        {
            var db = _redis.GetDatabase();
            var pingTime = await db.PingAsync();

            return HealthCheckResult.Healthy($"Redis is responsive (latency: {pingTime.TotalMilliseconds:F1}ms).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Redis ping failed: {ex.GetType().Name} - {ex.Message}");
        }
    }
}
