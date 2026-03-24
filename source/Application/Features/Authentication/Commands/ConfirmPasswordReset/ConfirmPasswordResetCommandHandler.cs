using Project.Application.Common.Interfaces;
using Project.Application.Common.Localizers;
using Project.Application.Common.Models;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Data.Repositories;
using Project.Domain.Interfaces.Services;
using Project.Domain.Notifications;

namespace Project.Application.Features.Commands.ConfirmPasswordReset;

public class ConfirmPasswordResetCommandHandler(
    IUnitOfWork unitOfWork, IMediator mediator, CultureLocalizer localizer,
    IRedisService redisService, IUserRepository userRepository,
    IEmailQueuePublisher emailQueuePublisher)
    : IRequestHandler<ConfirmPasswordResetCommand, ConfirmPasswordResetCommandResponse?>
{
    private readonly IUnitOfWork          _unitOfWork          = unitOfWork;
    private readonly IMediator            _mediator            = mediator;
    private readonly CultureLocalizer     _localizer           = localizer;
    private readonly IRedisService        _redisService        = redisService;
    private readonly IUserRepository      _userRepository      = userRepository;
    private readonly IEmailQueuePublisher _emailQueuePublisher = emailQueuePublisher;

    public async Task<ConfirmPasswordResetCommandResponse?> Handle(ConfirmPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var resetKey = $"reset:{command.Token}";
        var email    = await _redisService.GetAsync<string>(resetKey);

        if (email is null)
        {
            await _mediator.Publish(new DomainNotification("ConfirmPasswordReset", _localizer.Text("InvalidResetToken")), cancellationToken);
            return default;
        }

        var user = _userRepository.Get(x => x.Email == email.Trim('"'));

        if (user is null)
        {
            await _mediator.Publish(new DomainNotification("ConfirmPasswordReset", _localizer.Text("InvalidResetToken")), cancellationToken);
            return default;
        }

        user.HashedPassword = User.HashPassword(command.Request.NewPassword);
        _userRepository.Update(user);
        _unitOfWork.Commit();

        await _redisService.DeleteAsync(resetKey);

        var subject     = _localizer.Text("PasswordResetConfirmedSubject");
        var bodyContent = _localizer.Text("PasswordResetConfirmedBody", user.Username);

        await _emailQueuePublisher.PublishAsync(
            new EmailMessage { To = user.Email, Subject = subject, Body = bodyContent },
            cancellationToken);

        await _mediator.Publish(new DomainSuccessNotification("ConfirmPasswordReset", _localizer.Text("Success")), cancellationToken);

        return new ConfirmPasswordResetCommandResponse { Email = user.Email, Username = user.Username };
    }
}
