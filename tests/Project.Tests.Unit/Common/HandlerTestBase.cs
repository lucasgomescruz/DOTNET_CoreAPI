using System.Linq.Expressions;

namespace Project.Tests.Unit.Common;

/// <summary>
/// Abstract base class for all command-handler unit tests.
/// Provides pre-wired mocks for every dependency used across the Application layer,
/// plus convenience assertion helpers so individual test classes stay concise.
///
/// Usage:
///   public class MyHandlerTests : HandlerTestBase { … }
/// </summary>
public abstract class HandlerTestBase
{
    // ── Core mocks ──────────────────────────────────────────────────────────
    protected readonly Mock<IUserRepository>       MockUserRepository  = new();
    protected readonly Mock<IMediator>             MockMediator        = new();
    protected readonly Mock<IRedisService>         MockRedis           = new();
    protected readonly Mock<IEmailQueuePublisher>  MockEmailPublisher  = new();
    protected readonly Mock<IUnitOfWork>           MockUnitOfWork      = new();
    protected readonly Mock<ITokenService>         MockTokenService    = new();

    /// <summary>
    /// A <see cref="CultureLocalizer"/> that returns the resource key itself as the
    /// value. This removes resx dependency and lets tests assert on key names.
    /// </summary>
    protected readonly CultureLocalizer Localizer;

    // ── AppSettings ─────────────────────────────────────────────────────────
    private AppSettings _appSettings = new AppSettingsBuilder().Build();

    protected IOptions<AppSettings> AppSettingsOptions =>
        Options.Create(_appSettings);

    // ── Constructor ──────────────────────────────────────────────────────────
    protected HandlerTestBase()
    {
        Localizer = BuildPassthroughLocalizer();

        // Default: every Redis key returns null (no hit)
        MockRedis
            .Setup(r => r.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        // Default: SetAsync / DeleteAsync succeed silently
        MockRedis
            .Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        MockRedis
            .Setup(r => r.DeleteAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Default: email publisher always succeeds
        MockEmailPublisher
            .Setup(p => p.PublishAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default: mediator notifications succeed silently
        MockMediator
            .Setup(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Fluent configuration helpers ─────────────────────────────────────────

    /// <summary>Override the AppSettings used by the handler under test.</summary>
    protected void UseAppSettings(AppSettings settings) => _appSettings = settings;

    /// <summary>Sets a Redis key to return <paramref name="value"/>.</summary>
    protected void SetRedisKey(string key, string value)
    {
        MockRedis
            .Setup(r => r.GetAsync<string>(key))
            .ReturnsAsync(value);
    }

    /// <summary>Simulates an active email cooldown for <paramref name="email"/>.</summary>
    protected void SetEmailCooldown(string email) =>
        SetRedisKey($"email:cooldown:{email}", "1");

    /// <summary>
    /// Ensures the <see cref="IUserRepository.Get"/> mock returns
    /// <paramref name="user"/> for the given predicate.
    /// Pattern: the test sets up a concrete email/username match.
    /// </summary>
    protected void SetupUserForEmail(string email, User user)
    {
        MockUserRepository
            .Setup(r => r.Get(It.Is<Expression<Func<User, bool>>>(
                _ => EvaluatesTo(_, u => u.Email == email, new User(email, "P@ss1", email, Guid.NewGuid())))))
            .Returns(user);

        // Generic fallback so any expression that reaches the user is covered
        MockUserRepository
            .Setup(r => r.Get(It.IsAny<Expression<Func<User, bool>>>()))
            .Returns<Expression<Func<User, bool>>>(pred =>
            {
                var compiled = pred.Compile();
                return compiled(user) ? user : null;
            });
    }

    /// <summary>Registers a user with Get always returning null (no user found).</summary>
    protected void SetupNoUser()
    {
        MockUserRepository
            .Setup(r => r.Get(It.IsAny<Expression<Func<User, bool>>>()))
            .Returns((User?)null);
    }

    // ── Assertion helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that a <see cref="DomainNotification"/> (error) was published
    /// at least once with the given key.
    /// </summary>
    protected void ShouldHavePublishedNotification(string key)
    {
        MockMediator.Verify(
            m => m.Publish(
                It.Is<DomainNotification>(n => n.Key == key),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            $"Expected DomainNotification with key '{key}' to be published.");
    }

    /// <summary>
    /// Asserts that a <see cref="DomainSuccessNotification"/> was published
    /// at least once with the given key.
    /// </summary>
    protected void ShouldHavePublishedSuccess(string key)
    {
        MockMediator.Verify(
            m => m.Publish(
                It.Is<DomainSuccessNotification>(n => n.Key == key),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            $"Expected DomainSuccessNotification with key '{key}' to be published.");
    }

    /// <summary>
    /// Asserts that no email was dispatched.
    /// </summary>
    protected void ShouldNotHaveSentEmail()
    {
        MockEmailPublisher.Verify(
            p => p.PublishAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Expected no email to be sent.");
    }

    /// <summary>
    /// Asserts that exactly one email was dispatched to <paramref name="recipient"/>.
    /// </summary>
    protected void ShouldHaveSentEmailTo(string recipient)
    {
        MockEmailPublisher.Verify(
            p => p.PublishAsync(
                It.Is<EmailMessage>(m => m.To == recipient),
                It.IsAny<CancellationToken>()),
            Times.Once,
            $"Expected one email to be sent to '{recipient}'.");
    }

    /// <summary>
    /// Asserts that a Redis key was written with any value and any TTL.
    /// </summary>
    protected void ShouldHaveSetRedisKey(string key)
    {
        MockRedis.Verify(
            r => r.SetAsync(key, It.IsAny<string>(), It.IsAny<TimeSpan>()),
            Times.AtLeastOnce,
            $"Expected Redis key '{key}' to be set.");
    }

    /// <summary>
    /// Asserts that a Redis key was deleted at least once.
    /// </summary>
    protected void ShouldHaveDeletedRedisKey(string key)
    {
        MockRedis.Verify(
            r => r.DeleteAsync(key),
            Times.AtLeastOnce,
            $"Expected Redis key '{key}' to be deleted.");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static CultureLocalizer BuildPassthroughLocalizer()
    {
        var localizer = new Mock<IStringLocalizer>();

        localizer
            .Setup(l => l[It.IsAny<string>()])
            .Returns<string>(key => new LocalizedString(key, key));

        localizer
            .Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns<string, object[]>((key, _) => new LocalizedString(key, key));

        var factory = new Mock<IStringLocalizerFactory>();
        factory
            .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(localizer.Object);

        return new CultureLocalizer(factory.Object);
    }

    // Simple helper to avoid complex expression-tree matching in repo setups
    private static bool EvaluatesTo<T>(
        Expression<Func<T, bool>> expression, Func<T, bool> _matchFn, T _sample)
        => true; // We use the generic fallback instead
}
