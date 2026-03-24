using HotChocolate;
using Project.Application.Common.Interfaces;
using Project.Application.Common.Localizers;
using Project.Application.Common.Exceptions;
using Project.Application.Features.Commands.ConfirmPasswordReset;
using Project.Application.Features.Commands.ConfirmUser;
using Project.Application.Features.Commands.LoginUser;
using Project.Application.Features.Commands.RegisterUser;
using Project.Application.Features.Commands.RequestPasswordReset;
using Project.Application.Features.Commands.ResendConfirmation;
using Project.Domain.Interfaces.Data.Repositories;
using Project.Domain.Interfaces.Services;
using Project.Domain.Notifications;

namespace Project.WebApi.GraphQL;

public sealed class AuthMutation
{
    [GraphQLDescription("Register a new user account.")]
    public async Task<RegisterUserCommandResponse> Register(
        RegisterInput input,
        [Service] IMediator mediator,
        [Service] DomainNotificationHandler notifications)
    {
        try
        {
            var result = await mediator.Send(new RegisterUserCommand(new RegisterUserCommandRequest
            {
                Username = input.Username,
                Password = input.Password,
                Email    = input.Email
            }));

            ThrowIfHasErrors(notifications);
            return result ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Operation failed.").SetCode("OPERATION_FAILED").Build());
        }
        catch (ValidationException ex)
        {
            ThrowValidationErrors(ex);
            throw; // Should never reach here due to ThrowValidationErrors
        }
    }

    [GraphQLDescription("Authenticate and receive a JWT token.")]
    public async Task<LoginUserCommandResponse> Login(
        LoginInput input,
        [Service] IMediator mediator,
        [Service] DomainNotificationHandler notifications)
    {
        try
        {
            var result = await mediator.Send(new LoginUserCommand(new LoginUserCommandRequest
            {
                Login    = input.Login,
                Password = input.Password
            }));

            ThrowIfHasErrors(notifications);
            return result ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Operation failed.").SetCode("OPERATION_FAILED").Build());
        }
        catch (ValidationException ex)
        {
            ThrowValidationErrors(ex);
            throw; // Should never reach here due to ThrowValidationErrors
        }
    }

    [GraphQLDescription("Confirm a user account using the token sent by email.")]
    public async Task<ConfirmUserCommandResponse> ConfirmUser(
        string token,
        [Service] IMediator mediator,
        [Service] DomainNotificationHandler notifications)
    {
        try
        {
            var result = await mediator.Send(new ConfirmUserCommand(token));
            ThrowIfHasErrors(notifications);
            return result ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Operation failed.").SetCode("OPERATION_FAILED").Build());
        }
        catch (ValidationException ex)
        {
            ThrowValidationErrors(ex);
            throw; // Should never reach here due to ThrowValidationErrors
        }
    }

    [GraphQLDescription("Resend the account confirmation email.")]
    public async Task<ResendConfirmationCommandResponse> ResendConfirmation(
        ResendConfirmationInput input,
        [Service] IMediator mediator,
        [Service] DomainNotificationHandler notifications)
    {
        try
        {
            var result = await mediator.Send(new ResendConfirmationCommand(new ResendConfirmationCommandRequest
            {
                Email = input.Email
            }));

            ThrowIfHasErrors(notifications);
            return result ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Operation failed.").SetCode("OPERATION_FAILED").Build());
        }
        catch (ValidationException ex)
        {
            ThrowValidationErrors(ex);
            throw; // Should never reach here due to ThrowValidationErrors
        }
    }

    [GraphQLDescription("Request a password reset email.")]
    public async Task<RequestPasswordResetCommandResponse> ForgotPassword(
        ForgotPasswordInput input,
        [Service] IMediator mediator,
        [Service] DomainNotificationHandler notifications)
    {
        try
        {
            var result = await mediator.Send(new RequestPasswordResetCommand(new RequestPasswordResetCommandRequest
            {
                Email = input.Email
            }));

            ThrowIfHasErrors(notifications);
            return result ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Operation failed.").SetCode("OPERATION_FAILED").Build());
        }
        catch (ValidationException ex)
        {
            ThrowValidationErrors(ex);
            throw; // Should never reach here due to ThrowValidationErrors
        }
    }

    [GraphQLDescription("Reset the password using the token sent by email.")]
    public async Task<ConfirmPasswordResetCommandResponse> ResetPassword(
        string token,
        ResetPasswordInput input,
        [Service] IMediator mediator,
        [Service] DomainNotificationHandler notifications)
    {
        try
        {
            var result = await mediator.Send(new ConfirmPasswordResetCommand(token, new ConfirmPasswordResetCommandRequest
            {
                NewPassword = input.NewPassword
            }));

            ThrowIfHasErrors(notifications);
            return result ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Operation failed.").SetCode("OPERATION_FAILED").Build());
        }
        catch (ValidationException ex)
        {
            ThrowValidationErrors(ex);
            throw; // Should never reach here due to ThrowValidationErrors
        }
    }

    [GraphQLDescription("Update the authenticated user's username and invalidate the cache.")]
    public async Task<AuthenticatedUserType> UpdateUsername(
        UpdateUsernameInput input,
        [Service] IUser currentUser,
        [Service] IUserRepository userRepository,
        [Service] IUnitOfWork unitOfWork,
        [Service] IRedisService redisService,
        [Service] CultureLocalizer localizer)
    {
        if (currentUser.Id is null)
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(localizer.Text("LoginInvalidCredentials"))
                    .SetCode("UNAUTHENTICATED")
                    .Build());

        if (string.IsNullOrWhiteSpace(input.Username))
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid username.")
                    .SetCode("INVALID_INPUT")
                    .Build());

        var user = userRepository.Get(x => x.Id == currentUser.Id);
        if (user is null)
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("User not found.")
                    .SetCode("NOT_FOUND")
                    .Build());

        var conflict = userRepository.Get(x => x.Username == input.Username && x.Id != currentUser.Id);
        if (conflict is not null)
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Username already in use.")
                    .SetCode("CONFLICT")
                    .Build());

        user.UpdateUsername(input.Username);
        userRepository.Update(user);
        unitOfWork.Commit();

        var cacheKey = $"AuthenticatedUser:{currentUser.Id}";
        await redisService.DeleteAsync(cacheKey);

        return new AuthenticatedUserType(user.Id, user.Username, user.Email);
    }

    // ── Error helpers ─────────────────────────────────────────────────────────

    private static void ThrowIfHasErrors(DomainNotificationHandler handler)
    {
        if (!handler.HasNotification()) return;

        var errors = handler.GetNotifications()
            .Select(n => ErrorBuilder.New()
                .SetMessage(n.Value)
                .SetCode(n.Key)
                .Build())
            .ToArray();

        throw new GraphQLException(errors);
    }

    private static void ThrowValidationErrors(ValidationException ex)
    {
        var errors = ex.Errors
            .Select((msg, idx) => ErrorBuilder.New()
                .SetMessage(msg)
                .SetCode("VALIDATION_ERROR")
                .Build())
            .ToArray();

        throw new GraphQLException(errors);
    }
}
