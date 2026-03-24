namespace Project.Application.Features.Commands.ConfirmPasswordReset;

public record ConfirmPasswordResetCommandResponse
{
    public required string Email    { get; set; }
    public required string Username { get; set; }
}
