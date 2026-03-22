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
            var db = _redis.GetDatabase();
            var pong = await db.PingAsync();
            return pong.TotalMilliseconds >= 0
                ? HealthCheckResult.Healthy("Redis reachable")
                : HealthCheckResult.Unhealthy("Redis ping failed");
        }
        catch (System.Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis error: " + ex.Message);
        }
    }
}
