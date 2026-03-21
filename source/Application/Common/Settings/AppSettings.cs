namespace Project.Application.Common.Settings;

/// <summary>
/// Application-wide settings bound from the "AppSettings" configuration section.
/// </summary>
public class AppSettings
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public string ProjectName { get; set; } = "ProjectAPI";
    public string ProjectOwner { get; set; } = "Lucas Gomes Cruz";
    public string InterfaceUrl { get; set; } = ""; // Placeholder, no use yet
}
