using Project.Domain.Notifications;

namespace Project.Application.Features.Commands.ConfirmPasswordReset;

public class ConfirmPasswordResetCommand : Command<ConfirmPasswordResetCommandResponse>
{
    public string Token { get; set; }
    public ConfirmPasswordResetCommandRequest Request { get; set; }

    public ConfirmPasswordResetCommand(string token, ConfirmPasswordResetCommandRequest request)
    {
        Token   = token;
        Request = request;
    }
}
