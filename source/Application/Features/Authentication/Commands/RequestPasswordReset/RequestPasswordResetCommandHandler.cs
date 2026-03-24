using Project.Application.Common.Interfaces;
using Project.Application.Common.Localizers;
using Project.Application.Common.Models;
using Project.Application.Common.Settings;
using Project.Domain.Interfaces.Data.Repositories;
using Project.Domain.Interfaces.Services;
using Project.Domain.Notifications;
using Microsoft.Extensions.Options;

namespace Project.Application.Features.Commands.RequestPasswordReset;

public class RequestPasswordResetCommandHandler(
    IMediator mediator, CultureLocalizer localizer,
    IRedisService redisService, IEmailQueuePublisher emailQueuePublisher,
    IUserRepository userRepository, IOptions<AppSettings> appSettings)
    : IRequestHandler<RequestPasswordResetCommand, RequestPasswordResetCommandResponse?>
{
    private readonly IMediator            _mediator            = mediator;
    private readonly CultureLocalizer     _localizer           = localizer;
    private readonly IRedisService        _redisService        = redisService;
    private readonly IEmailQueuePublisher _emailQueuePublisher = emailQueuePublisher;
    private readonly IUserRepository      _userRepository      = userRepository;
    private readonly AppSettings          _appSettings         = appSettings.Value;

    public async Task<RequestPasswordResetCommandResponse?> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        // Always return success to prevent user enumeration
        var user = _userRepository.Get(x => x.Email == command.Request.Email);

        if (user is not null)
        {
            var expirationMinutes = 15;
            var token = Guid.NewGuid().ToString();
            var expiration = TimeSpan.FromMinutes(expirationMinutes);
            await _redisService.SetAsync($"reset:{token}", command.Request.Email, expiration);

            var resetUrl    = $"{_appSettings.BaseUrl}/api/v1/Authentication/ResetPassword/{token}";
            var subject     = _localizer.Text("PasswordResetSubject");
            var bodyContent = _localizer.Text("PasswordResetBody", user.Username, resetUrl, expirationMinutes);

            await _emailQueuePublisher.PublishAsync(
                new EmailMessage { To = command.Request.Email, Subject = subject, Body = bodyContent },
                cancellationToken);
        }

        await _mediator.Publish(new DomainSuccessNotification("RequestPasswordReset", _localizer.Text("Success")), cancellationToken);

        return new RequestPasswordResetCommandResponse { Email = command.Request.Email };
    }
}
