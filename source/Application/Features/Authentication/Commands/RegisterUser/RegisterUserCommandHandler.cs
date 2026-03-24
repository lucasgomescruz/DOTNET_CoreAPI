using Project.Application.Common.Interfaces;
using Project.Application.Common.Localizers;
using Project.Application.Common.Models;
using Project.Application.Common.Settings;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Data.Repositories;
using Project.Domain.Interfaces.Services;
using Project.Domain.Notifications;
using Microsoft.Extensions.Options;

namespace Project.Application.Features.Commands.RegisterUser
{
    public class RegisterUserCommandHandler(
        IUserRepository userRepository, IMediator mediator,
        CultureLocalizer localizer, IRedisService redisService,
        IEmailQueuePublisher emailQueuePublisher, IOptions<AppSettings> appSettings)
        : IRequestHandler<RegisterUserCommand, RegisterUserCommandResponse?>
    {
        private readonly IUserRepository      _userRepository      = userRepository;
        private readonly IMediator            _mediator            = mediator;
        private readonly CultureLocalizer     _localizer           = localizer;
        private readonly IRedisService        _redisService        = redisService;
        private readonly IEmailQueuePublisher _emailQueuePublisher = emailQueuePublisher;
        private readonly AppSettings          _appSettings         = appSettings.Value;

        public async Task<RegisterUserCommandResponse?> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
        {
            var expirationMinutes = 15;
            var token     = Guid.NewGuid().ToString();
            var tokenInfo = $"{command.Request.Email};{User.HashPassword(command.Request.Password)};{command.Request.Username}";
            var expiration = TimeSpan.FromMinutes(expirationMinutes);
            await _redisService.SetAsync(token, tokenInfo, expiration);
            await _redisService.SetAsync($"pending:{command.Request.Email}", token, expiration);

            var confirmationUrl = $"{_appSettings.BaseUrl}/api/v1/Authentication/Confirm/{token}";

            var subject     = _localizer.Text("ConfirmEmailSubject");
            var bodyContent = _localizer.Text("ConfirmEmailBody", command.Request.Username, confirmationUrl, expirationMinutes);

            await _emailQueuePublisher.PublishAsync(
                new EmailMessage { To = command.Request.Email, Subject = subject, Body = bodyContent },
                cancellationToken);

            await _mediator.Publish(new DomainSuccessNotification("RegisterUser", _localizer.Text("Success")), cancellationToken);

            return new RegisterUserCommandResponse { Username = command.Request.Username, Email = command.Request.Email };
        }
    }
}
