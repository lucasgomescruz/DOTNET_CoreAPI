using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Project.WebApi.Infrastructure;

public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConnection _connection;

    public RabbitMqHealthCheck(IConnection connection)
    {
        _connection = connection;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_connection == null || !_connection.IsOpen)
                return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ connection is closed or unavailable"));

            var sw = Stopwatch.StartNew();
            using var channel = _connection.CreateModel();
            sw.Stop();

            var data = new Dictionary<string, object>
            {
                ["channel_open"]    = channel.IsOpen,
                ["channel_latency_ms"] = sw.Elapsed.TotalMilliseconds
            };

            return Task.FromResult(channel.IsOpen
                ? HealthCheckResult.Healthy($"RabbitMQ reachable — channel opened in {sw.Elapsed.TotalMilliseconds:F1}ms", data)
                : HealthCheckResult.Degraded("RabbitMQ channel failed to open", data: data));
        }
        catch (System.Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"RabbitMQ unreachable: {ex.Message}"));
        }
    }
}
