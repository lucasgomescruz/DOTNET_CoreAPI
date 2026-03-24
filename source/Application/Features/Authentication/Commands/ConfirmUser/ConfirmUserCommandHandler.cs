using Project.Application.Common.Interfaces;
using Project.Application.Common.Localizers;
using Project.Application.Common.Models;
using Project.Domain.Constants;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Data.Repositories;
using Project.Domain.Interfaces.Services;
using Project.Domain.Notifications;

namespace Project.Application.Features.Commands.ConfirmUser;

public class ConfirmUserCommandHandler(
    IUnitOfWork unitOfWork, IMediator mediator, CultureLocalizer localizer,
    IRedisService redisService, IUserRepository userRepository, IEmailQueuePublisher emailQueuePublisher)
    : IRequestHandler<ConfirmUserCommand, ConfirmUserCommandResponse?>
{
    private readonly IUnitOfWork          _unitOfWork          = unitOfWork;
    private readonly IMediator            _mediator            = mediator;
    private readonly CultureLocalizer     _localizer           = localizer;
    private readonly IRedisService        _redisService        = redisService;
    private readonly IUserRepository      _userRepository      = userRepository;
    private readonly IEmailQueuePublisher _emailQueuePublisher = emailQueuePublisher;

    public async Task<ConfirmUserCommandResponse?> Handle(ConfirmUserCommand request, CancellationToken cancellationToken)
    {
        var tokenInfo = await _redisService.GetAsync<string>(request.Token);

        if (tokenInfo is null) {
            await _mediator.Publish(new DomainNotification("ConfirmUser", _localizer.Text("InvalidToken")), cancellationToken);
            return default;
        }

        var tokenInfoArray = tokenInfo.Trim('"').Split(";");

        var email = tokenInfoArray[0];
        var password = tokenInfoArray[1];
        var username = tokenInfoArray[2];

        var userExists = _userRepository.Get(x => x.Email == email);

        if (userExists is not null) {
            await _mediator.Publish(new DomainNotification("ConfirmUser", _localizer.Text("InvalidToken")), cancellationToken);
            return default;
        }

        await _redisService.DeleteAsync(request.Token);
        await _redisService.DeleteAsync($"pending:{email}");

        var user = new User(
            username: username,
            password: password,
            email: email,
            roleId: RoleConstants.User,
            isHashed: true
        );

        var inserted = _userRepository.Add(user);
        _unitOfWork.Commit();

        var subject = _localizer.Text("AccountConfirmedSubject");
        var bodyContent = _localizer.Text("AccountConfirmedBody", username);
        await _emailQueuePublisher.PublishAsync(
            new EmailMessage { To = email, Subject = subject, Body = bodyContent },
            cancellationToken);

        await _mediator.Publish(new DomainSuccessNotification("ConfirmUser", _localizer.Text("Success")), cancellationToken);
        var response = new ConfirmUserCommandResponse {
            Id = inserted.Id,
            Email = email,
            Username = username
        };
        return response;    
    }
}