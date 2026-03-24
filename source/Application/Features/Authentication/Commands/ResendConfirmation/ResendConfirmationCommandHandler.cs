using Project.Application.Common.Interfaces;
using Project.Application.Common.Localizers;
using Project.Application.Common.Models;
using Project.Application.Common.Settings;
using Project.Domain.Interfaces.Services;
using Project.Domain.Notifications;
using Microsoft.Extensions.Options;

namespace Project.Application.Features.Commands.ResendConfirmation;

public class ResendConfirmationCommandHandler(
    IMediator mediator, CultureLocalizer localizer,
    IRedisService redisService, IEmailQueuePublisher emailQueuePublisher,
    IOptions<AppSettings> appSettings)
    : IRequestHandler<ResendConfirmationCommand, ResendConfirmationCommandResponse?>
{
    private readonly IMediator            _mediator            = mediator;
    private readonly CultureLocalizer     _localizer           = localizer;
    private readonly IRedisService        _redisService        = redisService;
    private readonly IEmailQueuePublisher _emailQueuePublisher = emailQueuePublisher;
    private readonly AppSettings          _appSettings         = appSettings.Value;

    public async Task<ResendConfirmationCommandResponse?> Handle(ResendConfirmationCommand command, CancellationToken cancellationToken)
    {
        var pendingKey = $"pending:{command.Request.Email}";

        var cooldownSeconds = 60;
        if (!string.IsNullOrWhiteSpace(_appSettings.EmailCooldownSeconds) && int.TryParse(_appSettings.EmailCooldownSeconds, out var cfg) && cfg > 0)
            cooldownSeconds = cfg;
        var cooldownKey = $"email:cooldown:{command.Request.Email}";
        var cooldownExists = await _redisService.GetAsync<string>(cooldownKey);
        if (cooldownExists is not null)
        {
            await _mediator.Publish(new DomainNotification("ResendConfirmation", _localizer.Text("EmailCooldown", cooldownSeconds)), cancellationToken);
            return default;
        }

        var existingToken = await _redisService.GetAsync<string>(pendingKey);

        if (existingToken is null)
        {
            await _mediator.Publish(new DomainNotification("ResendConfirmation", _localizer.Text("NoPendingConfirmation")), cancellationToken);
            return default;
        }

        var tokenInfo = await _redisService.GetAsync<string>(existingToken.Trim('"'));

        if (tokenInfo is null)
        {
            await _mediator.Publish(new DomainNotification("ResendConfirmation", _localizer.Text("NoPendingConfirmation")), cancellationToken);
            return default;
        }

        // Remove the old token and reverse index
        await _redisService.DeleteAsync(existingToken.Trim('"'));
        await _redisService.DeleteAsync(pendingKey);

        // Issue a fresh token with the same registration data
        var expirationMinutes = 15;
        var newToken = Guid.NewGuid().ToString();
        var expiration = TimeSpan.FromMinutes(expirationMinutes);
        await _redisService.SetAsync(newToken, tokenInfo, expiration);
        await _redisService.SetAsync(pendingKey, newToken, expiration);

        var tokenInfoArray = tokenInfo.Trim('"').Split(";");
        var username = tokenInfoArray[2];

        var confirmationUrl = $"{_appSettings.BaseUrl}/api/v1/Authentication/Confirm/{newToken}";
        var subject     = _localizer.Text("ConfirmEmailSubject");
        var bodyContent = _localizer.Text("ConfirmEmailBody", username, confirmationUrl, expirationMinutes);

        // set cooldown to avoid spamming this email address
        await _redisService.SetAsync(cooldownKey, "1", TimeSpan.FromSeconds(cooldownSeconds));

        await _emailQueuePublisher.PublishAsync(
            new EmailMessage { To = command.Request.Email, Subject = subject, Body = bodyContent },
            cancellationToken);

        await _mediator.Publish(new DomainSuccessNotification("ResendConfirmation", _localizer.Text("Success")), cancellationToken);

        return new ResendConfirmationCommandResponse { Email = command.Request.Email };
    }
}
