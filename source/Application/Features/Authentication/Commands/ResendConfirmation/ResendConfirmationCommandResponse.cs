namespace Project.Application.Features.Commands.ResendConfirmation;

public record ResendConfirmationCommandResponse
{
    public required string Email { get; set; }
}
