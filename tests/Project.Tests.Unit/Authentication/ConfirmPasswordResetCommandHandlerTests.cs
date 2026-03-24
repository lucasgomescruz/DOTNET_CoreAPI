using System.Linq.Expressions;
using Project.Application.Features.Commands.ConfirmPasswordReset;

namespace Project.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for <see cref="ConfirmPasswordResetCommandHandler"/>.
/// Scenarios covered:
///   1. Valid token and registered user – updates password, deletes key, sends confirmation email.
///   2. Invalid / expired token – publishes notification, returns null.
///   3. Token valid but user not found in DB – publishes notification, returns null.
/// </summary>
public sealed class ConfirmPasswordResetCommandHandlerTests : HandlerTestBase
{
    private readonly ConfirmPasswordResetCommandHandler _sut;

    private const string Token       = "valid-reset-token";
    private const string Email       = "pwreset@example.com";
    private const string NewPassword = "NewPass@9876";
    private static string ResetKey   => $"reset:{Token}";

    public ConfirmPasswordResetCommandHandlerTests()
    {
        _sut = new ConfirmPasswordResetCommandHandler(
            MockUnitOfWork.Object,
            MockMediator.Object,
            Localizer,
            MockRedis.Object,
            MockUserRepository.Object,
            MockEmailPublisher.Object);
    }

    [Fact]
    public async Task Handle_ValidToken_UpdatesPasswordAndSendsConfirmationEmail()
    {
        // Arrange
        SetRedisKey(ResetKey, Email);
        var user = UserBuilder.WithPlainPassword(Email, "Old@1234");
        MockUserRepository
            .Setup(r => r.Get(It.IsAny<Expression<Func<User, bool>>>()))
            .Returns<Expression<Func<User, bool>>>(pred => pred.Compile()(user) ? user : null);
        MockUserRepository.Setup(r => r.Update(It.IsAny<User>())).Returns<User>(u => u);

        // Act
        var result = await _sut.Handle(
            new ConfirmPasswordResetCommand(Token, new ConfirmPasswordResetCommandRequest { NewPassword = NewPassword }),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(Email);

        // password on the entity was replaced
        user.HashedPassword.Should().NotBeNullOrWhiteSpace();
        user.VerifyPassword(NewPassword).Should().BeTrue("the stored hash should match the new password");

        MockUserRepository.Verify(r => r.Update(user), Times.Once);
        MockUnitOfWork.Verify(u => u.Commit(), Times.Once);
        ShouldHaveDeletedRedisKey(ResetKey);
        ShouldHaveSentEmailTo(Email);
        ShouldHavePublishedSuccess("ConfirmPasswordReset");
    }

    [Fact]
    public async Task Handle_InvalidToken_PublishesNotificationAndReturnsNull()
    {
        // Arrange – Redis returns null for reset key (default mock)

        // Act
        var result = await _sut.Handle(
            new ConfirmPasswordResetCommand("bad-token", new ConfirmPasswordResetCommandRequest { NewPassword = NewPassword }),
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
        ShouldHavePublishedNotification("ConfirmPasswordReset");
        MockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TokenValidButUserNotFound_PublishesNotificationAndReturnsNull()
    {
        // Arrange – token resolves to an email but no user in DB
        SetRedisKey(ResetKey, Email);
        MockUserRepository
            .Setup(r => r.Get(It.IsAny<Expression<Func<User, bool>>>()))
            .Returns((User?)null);

        // Act
        var result = await _sut.Handle(
            new ConfirmPasswordResetCommand(Token, new ConfirmPasswordResetCommandRequest { NewPassword = NewPassword }),
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
        ShouldHavePublishedNotification("ConfirmPasswordReset");
        MockUnitOfWork.Verify(u => u.Commit(), Times.Never);
    }
}
