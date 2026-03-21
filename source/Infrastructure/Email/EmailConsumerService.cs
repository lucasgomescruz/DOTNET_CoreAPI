using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Project.Application.Common.Models;
using Project.Domain.Interfaces.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Project.Infrastructure.Email;

/// <summary>
/// Long-running background service that consumes email messages from a RabbitMQ
/// queue and dispatches them via <see cref="IEmailService"/>.
/// Uses manual acknowledgement to guarantee at-least-once delivery: a message is
/// only acked after the email has been sent successfully.
/// </summary>
public sealed class EmailConsumerService(
    IConnection rabbitConnection,
    IOptions<RabbitMQSettings> settings,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private readonly IConnection      _connection  = rabbitConnection;
    private readonly RabbitMQSettings _settings    = settings.Value;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    private IModel? _channel;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = _connection.CreateModel();

        var queueName = string.IsNullOrWhiteSpace(_settings.EmailQueue)
            ? "email.queue"
            : _settings.EmailQueue;

        _channel.QueueDeclare(
            queue:      queueName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            arguments:  null);

        // Process one message at a time — prevents memory spikes and keeps
        // SMTP throughput predictable.
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleMessageAsync;

        _channel.BasicConsume(
            queue:       queueName,
            autoAck:     false,
            consumer:    consumer);

        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs ea)
    {
        ulong deliveryTag = ea.DeliveryTag;

        try
        {
            var json    = Encoding.UTF8.GetString(ea.Body.ToArray());
            var message = JsonSerializer.Deserialize<EmailMessage>(json);

            if (message is null)
            {
                _channel?.BasicReject(deliveryTag, requeue: false);
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            await emailService.SendEmailAsync(message.To, message.Subject, message.Body);

            _channel?.BasicAck(deliveryTag, multiple: false);
        }
        catch
        {
            // Requeue once; on a second failure the message will stay in the queue.
            // In production, pair with a Dead Letter Exchange (DLX) policy on the broker.
            _channel?.BasicNack(deliveryTag, multiple: false, requeue: true);
            throw;
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
