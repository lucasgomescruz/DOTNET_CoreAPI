using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Project.Application.Common.Interfaces;
using Project.Application.Common.Models;
using RabbitMQ.Client;

namespace Project.Infrastructure.Email;

/// <summary>
/// Publishes email messages to a RabbitMQ durable queue.
/// A new channel is created per publish call and disposed afterwards to remain
/// thread-safe without relying on channel pooling.
/// </summary>
public sealed class EmailQueuePublisher(
    IConnection rabbitConnection,
    IOptions<RabbitMQSettings> settings) : IEmailQueuePublisher
{
    private readonly IConnection     _connection = rabbitConnection;
    private readonly RabbitMQSettings _settings  = settings.Value;

    public Task PublishAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        using var channel = _connection.CreateModel();

        var queueName = string.IsNullOrWhiteSpace(_settings.EmailQueue)
            ? "email.queue"
            : _settings.EmailQueue;

        channel.QueueDeclare(
            queue:      queueName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            arguments:  null);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;       // survive broker restart
        properties.ContentType = "application/json";
        properties.ContentEncoding = "utf-8";

        channel.BasicPublish(
            exchange:   string.Empty,
            routingKey: queueName,
            basicProperties: properties,
            body:       body);

        return Task.CompletedTask;
    }
}
