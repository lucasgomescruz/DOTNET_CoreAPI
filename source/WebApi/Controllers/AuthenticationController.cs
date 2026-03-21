using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project.Application.Features.Commands.RegisterUser;
using Project.Application.Features.Commands.LoginUser;
using Project.Domain.Notifications;
using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Annotations;
using Project.Application.Features.Commands.ConfirmUser;

namespace Project.WebApi.Controllers
{
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public class AuthenticationController(INotificationHandler<DomainNotification> notifications,
                          INotificationHandler<DomainSuccessNotification> successNotifications,
                          IHttpContextAccessor httpContextAccessor,
                          IMediator mediatorHandler) : BaseController(notifications, successNotifications, mediatorHandler, httpContextAccessor)
    {
        private readonly IMediator _mediatorHandler = mediatorHandler;

        [HttpPost("Register")]
        [SwaggerOperation(Summary = "Register a new user.")]
        [ProducesResponseType(typeof(RegisterUserCommandResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Register([FromBody] RegisterUserCommandRequest request)
        {
            return Response(await _mediatorHandler.Send(new RegisterUserCommand(request)));
        }

        [HttpPost("Login")]
        [SwaggerOperation(Summary = "Authenticate a user.")]
        [ProducesResponseType(typeof(LoginUserCommandResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Login([FromBody] LoginUserCommandRequest request)
        {
            return Response(await _mediatorHandler.Send(new LoginUserCommand(request)));
        }

        [HttpGet("Confirm/{token}")]
        [SwaggerOperation(Summary = "Confirm a user.")]
        [ProducesResponseType(typeof(ConfirmUserCommandResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Confirm(string token)
        {
            return Response(await _mediatorHandler.Send(new ConfirmUserCommand(token)));
        }
    }
}
