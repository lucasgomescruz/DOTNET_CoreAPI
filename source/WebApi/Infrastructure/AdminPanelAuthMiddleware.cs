using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Project.WebApi.Infrastructure;

/// <summary>
/// Protects Swagger UI and GraphQL playground (Banana Cake Pop) with a shared admin password
/// in non-Development environments, or allows unrestricted access in Development.
/// 
/// Configuration:
///   - Environment variable: ADMIN_PANEL_PASSWORD
///   - Authentication method: X-Admin-Password header or ?adminPassword query parameter
///   - Example: curl -H "X-Admin-Password: your-secret" http://localhost:5000/swagger
/// </summary>
public class AdminPanelAuthMiddleware(RequestDelegate next, ILogger<AdminPanelAuthMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<AdminPanelAuthMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context, IWebHostEnvironment env)
    {
        // Only enforce in non-Development environments
        if (env.IsDevelopment())
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        // Check if this request is for Swagger UI or GraphQL playground
        bool isSwaggerRequest = path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
                            || path.StartsWith("/api-docs", StringComparison.OrdinalIgnoreCase)
                            || path == "/";  // root redirects to /swagger

        bool isGraphQLPlayground = path.StartsWith("/graphql", StringComparison.OrdinalIgnoreCase)
                                && !path.StartsWith("/graphql/", StringComparison.OrdinalIgnoreCase);  // Allow /graphql/ for actual queries

        if (!isSwaggerRequest && !isGraphQLPlayground)
        {
            await _next(context);
            return;
        }

        // For GET requests to Swagger/GraphQL UI, enforce authentication
        if (context.Request.Method != "GET")
        {
            await _next(context);
            return;
        }

        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PANEL_PASSWORD");

        // If no password is configured, deny access to prevent accidental exposure
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogWarning("Admin panel accessed in Production without ADMIN_PANEL_PASSWORD configured. Denying access.");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Admin panel is not configured." });
            return;
        }

        // Try to get the password from the request
        string? providedPassword = null;

        // Check header first (preferred method for security)
        if (context.Request.Headers.TryGetValue("X-Admin-Password", out var headerValue))
        {
            providedPassword = headerValue.ToString();
        }
        // Fall back to query parameter (less secure, but convenient)
        else if (context.Request.Query.TryGetValue("adminPassword", out var queryValue))
        {
            providedPassword = queryValue.ToString();
        }

        if (string.IsNullOrWhiteSpace(providedPassword) || !string.Equals(providedPassword, adminPassword, StringComparison.Ordinal))
        {
            _logger.LogWarning("Unauthorized admin panel access attempt from {remoteIp} for {path}", context.Connection.RemoteIpAddress, path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers["WWW-Authenticate"] = "Bearer realm=\"Admin Panel\"";
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized. Provide X-Admin-Password header or ?adminPassword query parameter." });
            return;
        }

        // Password matches; allow the request to proceed
        await _next(context);
    }
}
