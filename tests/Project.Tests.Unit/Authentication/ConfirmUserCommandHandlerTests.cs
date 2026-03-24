using System.Linq.Expressions;
using Project.Application.Features.Commands.ConfirmUser;

namespace Project.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for <see cref="ConfirmUserCommandHandler"/>.
/// Scenarios covered:
///   1. Valid token, email not registered – creates user, cleans Redis, sends email.
///   2. Invalid / expired token – publishes notification, returns null.
///   3. Email already registered – publishes notification, returns null.
/// </summary>
public sealed class ConfirmUserCommandHandlerTests : HandlerTestBase
{
    private readonly ConfirmUserCommandHandler _sut;

    private const string Token    = "valid-confirm-token";
    private const string Email    = "confirm@example.com";
    private const string Username = "newuser";
    private const string Hashed   = "hashed_password_placeholder"; // just a string in the token

    private static string TokenInfo => $"{Email};{Hashed};{Username}";

    public ConfirmUserCommandHandlerTests()
    {
        _sut = new ConfirmUserCommandHandler(
            MockUnitOfWork.Object,
            MockMediator.Object,
            Localizer,
            MockRedis.Object,
            MockUserRepository.Object,
            MockEmailPublisher.Object);
    }

    [Fact]
    public async Task Handle_ValidToken_UserNotExists_CreatesUserAndSendsWelcomeEmail()
    {
        // Arrange
        SetRedisKey(Token, TokenInfo);
        SetupNoUser();
        MockUserRepository.Setup(r => r.Add(It.IsAny<User>())).Returns<User>(u => u);

        // Act
        var result = await _sut.Handle(new ConfirmUserCommand(Token), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(Email);
        result.Username.Should().Be(Username);

        MockUserRepository.Verify(r => r.Add(It.Is<User>(u => u.Email == Email)), Times.Once);
        MockUnitOfWork.Verify(u => u.Commit(), Times.Once);
        ShouldHaveDeletedRedisKey(Token);
        ShouldHaveDeletedRedisKey($"pending:{Email}");
        ShouldHaveSentEmailTo(Email);
        ShouldHavePublishedSuccess("ConfirmUser");
    }

    [Fact]
    public async Task Handle_InvalidToken_PublishesNotificationAndReturnsNull()
    {
        // Arrange – Redis returns null for token (expired / never set)
        // (default mock already returns null for all keys)

        // Act
        var result = await _sut.Handle(new ConfirmUserCommand("expired-token"), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        ShouldHavePublishedNotification("ConfirmUser");
    }

    [Fact]
    public async Task Handle_EmailAlreadyRegistered_PublishesNotificationAndReturnsNull()
    {
        // Arrange – token valid but the email is already in the database
        SetRedisKey(Token, TokenInfo);
        var existingUser = UserBuilder.WithPlainPassword(Email, "Any@1234");
        MockUserRepository
            .Setup(r => r.Get(It.IsAny<Expression<Func<User, bool>>>()))
            .Returns<Expression<Func<User, bool>>>(pred => pred.Compile()(existingUser) ? existingUser : null);

        // Act
        var result = await _sut.Handle(new ConfirmUserCommand(Token), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        ShouldHavePublishedNotification("ConfirmUser");
        MockUserRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
    }
}
