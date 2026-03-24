namespace Project.Tests.Unit.Common.Builders;

/// <summary>
/// Fluent builder for <see cref="AppSettings"/> instances used in tests.
/// </summary>
public sealed class AppSettingsBuilder
{
    private string _baseUrl              = "http://localhost:5000";
    private string _projectName          = "TestProject";
    private string _projectOwner         = "Test";
    private string _interfaceUrl         = "";
    private string _emailCooldownSeconds = ""; // empty = use code default

    public AppSettingsBuilder WithBaseUrl(string url)                     { _baseUrl              = url;     return this; }
    public AppSettingsBuilder WithEmailCooldownSeconds(int seconds)       { _emailCooldownSeconds = seconds.ToString(); return this; }
    public AppSettingsBuilder WithNoEmailCooldownOverride()               { _emailCooldownSeconds = "";      return this; }

    public AppSettings Build() => new()
    {
        BaseUrl              = _baseUrl,
        ProjectName          = _projectName,
        ProjectOwner         = _projectOwner,
        InterfaceUrl         = _interfaceUrl,
        EmailCooldownSeconds = _emailCooldownSeconds,
    };
}
