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
            if (_connection != null && _connection.IsOpen)
            {
                // try creating a channel briefly
                using var channel = _connection.CreateModel();
                return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ reachable"));
            }

            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ connection closed"));
        }
        catch (System.Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ error: " + ex.Message));
        }
    }
}
