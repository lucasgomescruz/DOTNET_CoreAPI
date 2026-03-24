namespace Project.Application.Features.Commands.ConfirmPasswordReset;

public record ConfirmPasswordResetCommandRequest
{
    public string NewPassword { get; set; } = string.Empty;
}
