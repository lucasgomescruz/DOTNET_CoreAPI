using Project.Domain.Notifications;

namespace Project.Application.Features.Commands.RequestPasswordReset;

public class RequestPasswordResetCommand(RequestPasswordResetCommandRequest request) : Command<RequestPasswordResetCommandResponse>
{
    public RequestPasswordResetCommandRequest Request { get; set; } = request;
}
