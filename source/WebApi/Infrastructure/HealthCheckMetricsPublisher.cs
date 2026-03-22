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

    private static readonly Gauge DbGauge = Metrics.CreateGauge("webapi_db_up", "Database health (1 = up, 0 = down)");
    private static readonly Gauge RedisGauge = Metrics.CreateGauge("webapi_redis_up", "Redis health (1 = up, 0 = down)");
    private static readonly Gauge RabbitGauge = Metrics.CreateGauge("webapi_rabbitmq_up", "RabbitMQ health (1 = up, 0 = down)");

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

                // default to 0
                DbGauge.Set(0);
                RedisGauge.Set(0);
                RabbitGauge.Set(0);

                foreach (var entry in report.Entries)
                {
                    var name = entry.Key?.ToLowerInvariant() ?? string.Empty;
                    var healthy = entry.Value.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? 1 : 0;

                    if (name.Contains("database")) DbGauge.Set(healthy);
                    else if (name.Contains("redis")) RedisGauge.Set(healthy);
                    else if (name.Contains("rabbit")) RabbitGauge.Set(healthy);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing health metrics");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
