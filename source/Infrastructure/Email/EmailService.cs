using System.Net;
using System.Net.Mail;
using System.Reflection;
using Project.Application.Common.Settings;
using Project.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace Project.Infrastructure.Email;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly AppSettings _appSettings;
    private readonly SmtpClient _smtpClient;

    public EmailService(IOptions<EmailSettings> emailSettings, IOptions<AppSettings> appSettings)
    {
        _emailSettings = emailSettings.Value;
        _appSettings = appSettings.Value;
        _emailSettings = emailSettings.Value;

        _smtpClient = new SmtpClient(_emailSettings.Server)
        {
            Port = _emailSettings.Port,
            Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
            EnableSsl = _emailSettings.EnableSsl,
        };
    }

    public async Task SendEmailAsync(string to, string subject, string bodyContent)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(x => x.EndsWith("Template.html", StringComparison.OrdinalIgnoreCase));
        string template = await GetEmbeddedResourceContentAsync(resourceName);
        string body = template
            .Replace("{{bodyContent}}", bodyContent)
            .Replace("{{projectName}}", _appSettings.ProjectName)
            .Replace("{{projectOwner}}", _appSettings.ProjectOwner);

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_emailSettings.Username),
            Subject = _appSettings.ProjectName + " - " + subject,
            Body = body,
            IsBodyHtml = true,
        };
        mailMessage.To.Add(to);

        await _smtpClient.SendMailAsync(mailMessage);
    }

    private static async Task<string> GetEmbeddedResourceContentAsync(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException($"Resource '{resourceName}' not found.");
        using StreamReader reader = new(stream);
        return await reader.ReadToEndAsync();
    }
}