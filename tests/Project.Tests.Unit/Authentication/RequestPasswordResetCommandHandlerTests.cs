using System.Linq.Expressions;
using Project.Application.Features.Commands.RequestPasswordReset;

namespace Project.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for <see cref="RequestPasswordResetCommandHandler"/>.
/// Scenarios covered:
///   1. User found, no cooldown – sends reset email, sets cooldown.
///   2. User found, cooldown active – returns success silently (anti-enumeration).
///   3. User not found – returns success silently (anti-enumeration).
/// </summary>
public sealed class RequestPasswordResetCommandHandlerTests : HandlerTestBase
{
    private readonly RequestPasswordResetCommandHandler _sut;

    private const string Email = "reset@example.com";

    public RequestPasswordResetCommandHandlerTests()
    {
        _sut = new RequestPasswordResetCommandHandler(
            MockMediator.Object,
            Localizer,
            MockRedis.Object,
            MockEmailPublisher.Object,
            MockUserRepository.Object,
            AppSettingsOptions);
    }

    [Fact]
    public async Task Handle_UserFound_NoCooldown_SendsEmailAndSetsCooldown()
    {
        // Arrange
        var user = UserBuilder.WithPlainPassword(Email, "Any@1234");
        MockUserRepository
            .Setup(r => r.Get(It.IsAny<Expression<Func<User, bool>>>()))
            .Returns<Expression<Func<User, bool>>>(pred => pred.Compile()(user) ? user : null);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(Email);

        // reset token was stored
        MockRedis.Verify(
            r => r.SetAsync(It.Is<string>(k => k.StartsWith("reset:")), It.IsAny<string>(), It.IsAny<TimeSpan>()),
            Times.Once, "Reset token should be stored in Redis.");

        ShouldHaveSetRedisKey($"email:cooldown:{Email}");
        ShouldHaveSentEmailTo(Email);
        ShouldHavePublishedSuccess("RequestPasswordReset");
    }

    [Fact]
    public async Task Handle_UserFound_CooldownActive_ReturnsSuccessWithoutSendingEmail()
    {
        // Arrange
        var user = UserBuilder.WithPlainPassword(Email, "Any@1234");
        MockUserRepository
            .Setup(r => r.Get(It.IsAny<Expression<Func<User, bool>>>()))
            .Returns<Expression<Func<User, bool>>>(pred => pred.Compile()(user) ? user : null);
        SetEmailCooldown(Email);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert – must still succeed (anti-enumeration) but without sending email
        result.Should().NotBeNull();
        ShouldNotHaveSentEmail();
        ShouldHavePublishedSuccess("RequestPasswordReset");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsSuccessWithoutSendingEmail()
    {
        // Arrange – no user in repo (default mock returns null)

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert – silent success for anti-enumeration
        result.Should().NotBeNull();
        ShouldNotHaveSentEmail();
        ShouldHavePublishedSuccess("RequestPasswordReset");
    }

    private static RequestPasswordResetCommand Command() =>
        new(new RequestPasswordResetCommandRequest { Email = Email });
}
