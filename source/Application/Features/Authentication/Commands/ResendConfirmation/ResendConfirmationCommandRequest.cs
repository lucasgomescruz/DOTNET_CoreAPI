namespace Project.Application.Features.Commands.ResendConfirmation;

public record ResendConfirmationCommandRequest
{
    public string Email { get; set; } = string.Empty;
}
