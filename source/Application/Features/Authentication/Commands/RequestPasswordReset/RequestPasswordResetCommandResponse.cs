namespace Project.Application.Features.Commands.RequestPasswordReset;

public record RequestPasswordResetCommandResponse
{
    public required string Email { get; set; }
}
