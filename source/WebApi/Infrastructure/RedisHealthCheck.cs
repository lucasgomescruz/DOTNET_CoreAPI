using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Project.WebApi.Infrastructure;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return HealthCheckResult.Unhealthy("Redis connection is not open");

            var db    = _redis.GetDatabase();
            var pong  = await db.PingAsync();
            var data  = new Dictionary<string, object>
            {
                ["latency_ms"]  = pong.TotalMilliseconds,
                ["connected_endpoints"] = _redis.GetCounters().TotalOutstanding
            };

            return pong.TotalMilliseconds >= 0
                ? HealthCheckResult.Healthy($"Redis reachable — latency {pong.TotalMilliseconds:F1}ms", data)
                : HealthCheckResult.Degraded("Redis ping returned unexpected result", data: data);
        }
        catch (System.Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Redis unreachable: {ex.Message}");
        }
    }
}
