using Project.Application.Common.Models;

namespace Project.Application.Common.Interfaces;

/// <summary>
/// Publishes an email message to the message queue for asynchronous delivery.
/// </summary>
public interface IEmailQueuePublisher
{
    Task PublishAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
