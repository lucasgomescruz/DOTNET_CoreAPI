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
    [Authorize]
    public class AuthenticationController(
        INotificationHandler<DomainNotification> notifications,
        INotificationHandler<DomainSuccessNotification> successNotifications,
        IHttpContextAccessor httpContextAccessor,
        IMediator mediatorHandler,
        Project.Application.Common.Interfaces.IUser currentUser,
        Project.Domain.Interfaces.Data.Repositories.IUserRepository userRepository,
        Project.Application.Common.Interfaces.IUnitOfWork unitOfWork,
        Project.Domain.Interfaces.Services.IRedisService redisService)
        : BaseController(notifications, successNotifications, mediatorHandler, httpContextAccessor)
    {
        private readonly IMediator _mediatorHandler = mediatorHandler;
        private readonly Project.Application.Common.Interfaces.IUser _currentUser = currentUser;
        private readonly Project.Domain.Interfaces.Data.Repositories.IUserRepository _userRepository = userRepository;
        private readonly Project.Application.Common.Interfaces.IUnitOfWork _unitOfWork = unitOfWork;
        private readonly Project.Domain.Interfaces.Services.IRedisService _redisService = redisService;

        private const int CacheMinutes = 30;

        [AllowAnonymous]
        [HttpPost("Register")]
        [SwaggerOperation(Summary = "Register a new user.")]
        [ProducesResponseType(typeof(RegisterUserCommandResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Register([FromBody] RegisterUserCommandRequest request)
        {
            return Response(await _mediatorHandler.Send(new RegisterUserCommand(request)));
        }

        [AllowAnonymous]
        [HttpPost("Login")]
        [SwaggerOperation(Summary = "Authenticate a user.")]
        [ProducesResponseType(typeof(LoginUserCommandResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Login([FromBody] LoginUserCommandRequest request)
        {
            return Response(await _mediatorHandler.Send(new LoginUserCommand(request)));
        }

        [AllowAnonymous]
        [HttpGet("Confirm/{token}")]
        [SwaggerOperation(Summary = "Confirm a user.")]
        [ProducesResponseType(typeof(ConfirmUserCommandResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Confirm(string token)
        {
            return Response(await _mediatorHandler.Send(new ConfirmUserCommand(token)));
        }

        [HttpGet("GetAuthenticatedUser")]
        [SwaggerOperation(Summary = "Get authenticated user data with Redis cache.")]
        [ProducesResponseType(typeof(AuthenticatedUserResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAuthenticatedUser()
        {
            if (_currentUser.Id == null)
                return Unauthorized("Usuário não autenticado.");

            var cacheKey = $"AuthenticatedUser:{_currentUser.Id}";

            var cached = await _redisService.GetAsync<AuthenticatedUserResponse>(cacheKey);
            if (cached != null)
                return Ok(cached);

            var userEntity = _userRepository.Get(x => x.Id == _currentUser.Id);
            if (userEntity == null)
                return NotFound("Usuário não encontrado.");

            var payload = new AuthenticatedUserResponse
            {
                Id = userEntity.Id,
                Username = userEntity.Username,
                Email = userEntity.Email
            };

            await _redisService.SetAsync(cacheKey, payload, TimeSpan.FromMinutes(CacheMinutes));

            return Ok(payload);
        }

        [HttpPut("UpdateUsername")]
        [SwaggerOperation(Summary = "Update authenticated user username and invalidate cache.")]
        [ProducesResponseType(typeof(AuthenticatedUserResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateUsername([FromBody] UpdateUsernameRequest request)
        {
            if (_currentUser.Id == null)
                return Unauthorized("Usuário não autenticado.");

            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest("Nome de usuário inválido.");

            var userEntity = _userRepository.Get(x => x.Id == _currentUser.Id);
            if (userEntity == null)
                return NotFound("Usuário não encontrado.");

            var existingConflict = _userRepository.Get(x => x.Username == request.Username && x.Id != _currentUser.Id);
            if (existingConflict != null)
                return Conflict("O username já está em uso.");

            userEntity.UpdateUsername(request.Username);
            _userRepository.Update(userEntity);
            _unitOfWork.Commit();

            var cacheKey = $"AuthenticatedUser:{_currentUser.Id}";
            await _redisService.DeleteAsync(cacheKey);

            var payload = new AuthenticatedUserResponse
            {
                Id = userEntity.Id,
                Username = userEntity.Username,
                Email = userEntity.Email
            };

            return Ok(payload);
        }

        public record AuthenticatedUserResponse
        {
            public Guid Id { get; init; }
            public string Username { get; init; } = string.Empty;
            public string Email { get; init; } = string.Empty;
        }

        public class UpdateUsernameRequest
        {
            public string Username { get; set; } = string.Empty;
        }
    }
}
