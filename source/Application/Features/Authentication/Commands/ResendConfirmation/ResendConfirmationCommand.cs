using Project.Domain.Notifications;

namespace Project.Application.Features.Commands.ResendConfirmation;

public class ResendConfirmationCommand(ResendConfirmationCommandRequest request) : Command<ResendConfirmationCommandResponse>
{
    public ResendConfirmationCommandRequest Request { get; set; } = request;
}
