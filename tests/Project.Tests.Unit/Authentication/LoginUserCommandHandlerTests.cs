using System.Linq.Expressions;
using Project.Application.Features.Commands.LoginUser;

namespace Project.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for <see cref="LoginUserCommandHandler"/>.
/// Scenarios covered:
///   1. Valid credentials – returns JWT token response.
///   2. User not found – publishes notification, returns null.
///   3. Invalid password – publishes notification, returns null.
/// </summary>
public sealed class LoginUserCommandHandlerTests : HandlerTestBase
{
    private readonly LoginUserCommandHandler _sut;

    private const string Email    = "login@example.com";
    private const string Username = "loginuser";
    private const string Password = "Test@1234";
    private const string FakeJwt  = "header.payload.signature";

    public LoginUserCommandHandlerTests()
    {
        _sut = new LoginUserCommandHandler(
            MockUserRepository.Object,
            MockTokenService.Object,
            MockMediator.Object,
            Localizer);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokenResponse()
    {
        // Arrange
        var user = UserBuilder.WithPlainPassword(Email, Password);
        MockUserRepository
            .Setup(r => r.Get(It.IsAny<Expression<Func<User, bool>>>()))
            .Returns<Expression<Func<User, bool>>>(pred => pred.Compile()(user) ? user : null);

        MockTokenService
            .Setup(t => t.GenerateToken(user))
            .Returns(FakeJwt);

        // Act
        var result = await _sut.Handle(new LoginUserCommand(
            new LoginUserCommandRequest { Login = Email, Password = Password }),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be(FakeJwt);
        ShouldHavePublishedSuccess("LoginUser");
    }

    [Fact]
    public async Task Handle_UserNotFound_PublishesNotificationAndReturnsNull()
    {
        // Arrange
        MockUserRepository
            .Setup(r => r.Get(It.IsAny<Expression<Func<User, bool>>>()))
            .Returns((User?)null);

        // Act
        var result = await _sut.Handle(new LoginUserCommand(
            new LoginUserCommandRequest { Login = "ghost@example.com", Password = Password }),
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
        ShouldHavePublishedNotification("LoginUser");
    }

    [Fact]
    public async Task Handle_WrongPassword_PublishesNotificationAndReturnsNull()
    {
        // Arrange – user exists but password will not match
        var user = UserBuilder.WithPlainPassword(Email, "Correct@1234");
        MockUserRepository
            .Setup(r => r.Get(It.IsAny<Expression<Func<User, bool>>>()))
            .Returns<Expression<Func<User, bool>>>(pred => pred.Compile()(user) ? user : null);

        // Act
        var result = await _sut.Handle(new LoginUserCommand(
            new LoginUserCommandRequest { Login = Email, Password = "Wrong@9999" }),
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
        ShouldHavePublishedNotification("LoginUser");
    }
}
