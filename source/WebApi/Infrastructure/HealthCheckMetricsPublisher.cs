using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Project.WebApi.Infrastructure;

public class HealthCheckMetricsPublisher : BackgroundService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<HealthCheckMetricsPublisher> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(15);

    // Status gauges (1 = healthy, 0 = degraded/unhealthy)
    private static readonly Gauge DbGauge      = Metrics.CreateGauge("webapi_db_up",       "Database health (1 = up, 0 = down)");
    private static readonly Gauge RedisGauge   = Metrics.CreateGauge("webapi_redis_up",    "Redis health (1 = up, 0 = down)");
    private static readonly Gauge RabbitGauge  = Metrics.CreateGauge("webapi_rabbitmq_up", "RabbitMQ health (1 = up, 0 = down)");
    private static readonly Gauge OverallGauge = Metrics.CreateGauge("webapi_health_up",   "Overall health — all checks healthy (1) or at least one failing (0)");

    // Duration gauges (milliseconds for last health check execution)
    private static readonly Gauge DbDurationGauge     = Metrics.CreateGauge("webapi_db_duration_ms",       "Database health check duration in ms");
    private static readonly Gauge RedisDurationGauge  = Metrics.CreateGauge("webapi_redis_duration_ms",    "Redis health check duration in ms");
    private static readonly Gauge RabbitDurationGauge = Metrics.CreateGauge("webapi_rabbitmq_duration_ms", "RabbitMQ health check duration in ms");

    public HealthCheckMetricsPublisher(HealthCheckService healthCheckService, ILogger<HealthCheckMetricsPublisher> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var report = await _healthCheckService.CheckHealthAsync(stoppingToken);

                // Reset to 0 before applying current values
                DbGauge.Set(0);
                RedisGauge.Set(0);
                RabbitGauge.Set(0);

                foreach (var entry in report.Entries)
                {
                    var name    = entry.Key?.ToLowerInvariant() ?? string.Empty;
                    var healthy = entry.Value.Status == HealthStatus.Healthy ? 1 : 0;
                    var durationMs = entry.Value.Duration.TotalMilliseconds;

                    if (name.Contains("database"))
                    {
                        DbGauge.Set(healthy);
                        DbDurationGauge.Set(durationMs);
                    }
                    else if (name.Contains("redis"))
                    {
                        RedisGauge.Set(healthy);
                        RedisDurationGauge.Set(durationMs);
                    }
                    else if (name.Contains("rabbit"))
                    {
                        RabbitGauge.Set(healthy);
                        RabbitDurationGauge.Set(durationMs);
                    }

                    if (entry.Value.Status != HealthStatus.Healthy)
                    {
                        _logger.LogWarning(
                            "Health check {CheckName} is {Status}: {Description}",
                            entry.Key, entry.Value.Status, entry.Value.Description ?? "no description");
                    }
                }

                var allHealthy = report.Entries.Values.All(e => e.Status == HealthStatus.Healthy);
                OverallGauge.Set(allHealthy ? 1 : 0);

                _logger.LogDebug(
                    "Health check cycle completed — overall={Overall} db={Db}ms redis={Redis}ms rabbit={Rabbit}ms",
                    allHealthy ? "OK" : "DEGRADED",
                    DbDurationGauge.Value,
                    RedisDurationGauge.Value,
                    RabbitDurationGauge.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing health check metrics");
                OverallGauge.Set(0);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
