using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Project.WebApi.Infrastructure;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        var request = context.Request;
        string? requestBody = null;

        try
        {
            if (request.ContentLength > 0 && request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                request.EnableBuffering();
                using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }
        }
        catch
        {
            // Ignore read failures; logging should not break requests
        }

        var userId = context.User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
        var username = context.User?.FindFirst(ClaimTypes.Name)?.Value
                   ?? context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? context.User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                   ?? context.User?.FindFirst("sub")?.Value;

        _logger.LogInformation("Incoming request {method} {path} QueryString={qs} UserId={userId} Username={username} Body={body}",
            request.Method, request.Path, request.QueryString, userId, username, requestBody);

        var originalBodyStream = context.Response.Body;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            string responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            sw.Stop();
            _logger.LogInformation("Outgoing response {statusCode} for {method} {path} in {duration}ms UserId={userId} Username={username} ResponseBody={response}",
                context.Response.StatusCode, request.Method, request.Path, sw.ElapsedMilliseconds, userId, username, responseText);

            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Unhandled exception for {method} {path} in {duration}ms UserId={userId}", request.Method, request.Path, sw.ElapsedMilliseconds, userId);
            context.Response.Body = originalBodyStream;
            throw;
        }
    }
}
