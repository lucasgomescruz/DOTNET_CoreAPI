namespace Project.Infrastructure.Email;

public class RabbitMQSettings
{
    public required string Connection { get; set; }
    public string EmailQueue { get; set; } = "email.queue";
}
