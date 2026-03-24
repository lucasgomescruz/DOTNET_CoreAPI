namespace Project.Application.Features.Commands.RequestPasswordReset;

public record RequestPasswordResetCommandRequest
{
    public string Email { get; set; } = string.Empty;
}
