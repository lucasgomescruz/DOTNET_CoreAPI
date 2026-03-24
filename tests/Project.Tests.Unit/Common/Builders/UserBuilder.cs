using Project.Domain.Constants;

namespace Project.Tests.Unit.Common.Builders;

/// <summary>
/// Fluent builder for <see cref="User"/> instances used in tests.
/// Exposes only the fields necessary for test readability.
/// </summary>
public sealed class UserBuilder
{
    private string _username = "testuser";
    private string _password = "Test@1234";
    private string _email    = "test@example.com";
    private Guid   _roleId   = RoleConstants.User;

    public UserBuilder WithUsername(string username) { _username = username; return this; }
    public UserBuilder WithPassword(string password) { _password = password; return this; }
    public UserBuilder WithEmail(string email)       { _email    = email;    return this; }
    public UserBuilder WithRoleId(Guid roleId)       { _roleId   = roleId;   return this; }

    public User Build() => new(_username, _password, _email, _roleId);

    /// <summary>Shortcut: builds and returns a user with reasonable defaults.</summary>
    public static User Default() => new UserBuilder().Build();

    /// <summary>Returns a confirmed user whose stored password is the BCrypt hash of <paramref name="plaintext"/>.</summary>
    public static User WithPlainPassword(string email, string plaintext) =>
        new UserBuilder().WithEmail(email).WithPassword(plaintext).Build();
}
