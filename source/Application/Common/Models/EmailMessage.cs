namespace Project.Application.Common.Models;

/// <summary>
/// Represents an email message that travels through the message queue.
/// </summary>
public sealed record EmailMessage
{
    public required string To      { get; init; }
    public required string Subject { get; init; }
    public required string Body    { get; init; }
}
