using System.Linq.Expressions;
using Project.Application.Features.Commands.RegisterUser;

namespace Project.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for <see cref="RegisterUserCommandHandler"/>.
/// Scenarios covered:
///   1. Happy path – stores tokens, sets cooldown, sends confirmation email.
///   2. Email cooldown active – returns null, publishes notification, no email sent.
/// </summary>
public sealed class RegisterUserCommandHandlerTests : HandlerTestBase
{
    private readonly RegisterUserCommandHandler _sut;

    private const string Email    = "user@example.com";
    private const string Username = "testuser";
    private const string Password = "Test@1234";

    public RegisterUserCommandHandlerTests()
    {
        _sut = new RegisterUserCommandHandler(
            MockUserRepository.Object,
            MockMediator.Object,
            Localizer,
            MockRedis.Object,
            MockEmailPublisher.Object,
            AppSettingsOptions);
    }

    [Fact]
    public async Task Handle_NoCooldown_StoresTokenAndSendsConfirmationEmail()
    {
        // Arrange – Redis returns null for both cooldown and pending keys
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(Email);
        result.Username.Should().Be(Username);

        // token + pending index stored
        MockRedis.Verify(
            r => r.SetAsync(It.IsAny<string>(), It.Is<string>(v => v.Contains(Email)), It.IsAny<TimeSpan>()),
            Times.AtLeastOnce, "Registration token should be stored in Redis.");

        ShouldHaveSetRedisKey($"pending:{Email}");
        ShouldHaveSetRedisKey($"email:cooldown:{Email}");
        ShouldHaveSentEmailTo(Email);
        ShouldHavePublishedSuccess("RegisterUser");
    }

    [Fact]
    public async Task Handle_EmailCooldownActive_ReturnsNullAndPublishesNotification()
    {
        // Arrange – cooldown key exists
        SetEmailCooldown(Email);
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        ShouldHavePublishedNotification("RegisterUser");
        ShouldNotHaveSentEmail();
    }

    [Fact]
    public async Task Handle_ConfiguredCooldownFromAppSettings_IsRespected()
    {
        // Arrange – use a configured cooldown of 120 s
        UseAppSettings(new AppSettingsBuilder().WithEmailCooldownSeconds(120).Build());
        var sut = new RegisterUserCommandHandler(
            MockUserRepository.Object,
            MockMediator.Object,
            Localizer,
            MockRedis.Object,
            MockEmailPublisher.Object,
            AppSettingsOptions);

        var command = BuildCommand();

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert – cooldown key must have been set (TTL verified via SetAsync call)
        result.Should().NotBeNull();
        MockRedis.Verify(
            r => r.SetAsync($"email:cooldown:{Email}", It.IsAny<string>(),
                It.Is<TimeSpan>(ts => ts.TotalSeconds == 120)),
            Times.Once,
            "Cooldown TTL should match the configured value.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static RegisterUserCommand BuildCommand(
        string email    = Email,
        string username = Username,
        string password = Password) =>
        new(new RegisterUserCommandRequest { Email = email, Username = username, Password = password });
}
