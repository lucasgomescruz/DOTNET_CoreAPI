using Project.Application.Features.Commands.ResendConfirmation;

namespace Project.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for <see cref="ResendConfirmationCommandHandler"/>.
/// Scenarios covered:
///   1. Email cooldown active – returns null, publishes notification.
///   2. No pending registration – returns null, publishes notification.
///   3. Pending token expired in Redis – returns null, publishes notification.
///   4. Happy path – issues new token, sends email, sets cooldown.
/// </summary>
public sealed class ResendConfirmationCommandHandlerTests : HandlerTestBase
{
    private readonly ResendConfirmationCommandHandler _sut;

    private const string Email         = "resend@example.com";
    private const string Username      = "resenduser";
    private const string Hashed        = "hashed_pw";
    private const string OldToken      = "old-token-guid";
    private static string PendingKey   => $"pending:{Email}";
    private static string TokenInfo    => $"{Email};{Hashed};{Username}";

    public ResendConfirmationCommandHandlerTests()
    {
        _sut = new ResendConfirmationCommandHandler(
            MockMediator.Object,
            Localizer,
            MockRedis.Object,
            MockEmailPublisher.Object,
            AppSettingsOptions);
    }

    [Fact]
    public async Task Handle_CooldownActive_ReturnsNullAndPublishesNotification()
    {
        // Arrange
        SetEmailCooldown(Email);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        ShouldHavePublishedNotification("ResendConfirmation");
        ShouldNotHaveSentEmail();
    }

    [Fact]
    public async Task Handle_NoPendingRegistration_ReturnsNullAndPublishesNotification()
    {
        // Arrange – no cooldown, no pending key (all Redis returns null by default)

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        ShouldHavePublishedNotification("ResendConfirmation");
        ShouldNotHaveSentEmail();
    }

    [Fact]
    public async Task Handle_PendingKeyExistsButTokenExpired_ReturnsNullAndPublishesNotification()
    {
        // Arrange – pending key points to old token, but old token is no longer in Redis
        SetRedisKey(PendingKey, OldToken);
        // OldToken key remains null (default mock)

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        ShouldHavePublishedNotification("ResendConfirmation");
        ShouldNotHaveSentEmail();
    }

    [Fact]
    public async Task Handle_ValidPendingRegistration_IssuesNewTokenSendsEmailSetsCooldown()
    {
        // Arrange
        SetRedisKey(PendingKey, OldToken);
        SetRedisKey(OldToken, TokenInfo);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(Email);

        // old keys cleaned
        ShouldHaveDeletedRedisKey(OldToken);
        ShouldHaveDeletedRedisKey(PendingKey);

        // new keys written
        ShouldHaveSetRedisKey(PendingKey);          // new pending index
        ShouldHaveSetRedisKey($"email:cooldown:{Email}");

        ShouldHaveSentEmailTo(Email);
        ShouldHavePublishedSuccess("ResendConfirmation");
    }

    private static ResendConfirmationCommand Command() =>
        new(new ResendConfirmationCommandRequest { Email = Email });
}
