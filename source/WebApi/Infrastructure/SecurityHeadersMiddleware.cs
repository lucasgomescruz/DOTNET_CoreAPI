using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Project.WebApi.Infrastructure;

/// <summary>
/// Adds industry-standard security headers to every HTTP response and removes
/// information-leaking server identification headers.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME-type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Deny rendering inside frames (clickjacking protection)
        headers["X-Frame-Options"] = "DENY";

        // Legacy XSS filter (still respected by older browsers)
        headers["X-XSS-Protection"] = "1; mode=block";

        // Control referrer information
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Determine if this request is for Swagger UI or GraphQL playground, and running in Development
        var env = context.RequestServices.GetService<IWebHostEnvironment>();
        var isSwaggerRequest  = context.Request.Path.StartsWithSegments("/swagger",  StringComparison.OrdinalIgnoreCase);
        var isGraphQlRequest  = context.Request.Path.StartsWithSegments("/graphql",  StringComparison.OrdinalIgnoreCase);

        // CSP: default restrictive policy
        var csp = "default-src 'self'; frame-ancestors 'none'; form-action 'self';";

        if (env?.IsDevelopment() == true)
        {
            if (isSwaggerRequest)
            {
                // Swagger UI: needs inline scripts/styles
                csp = "default-src 'self'; frame-ancestors 'none'; form-action 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline';";
            }
            else if (isGraphQlRequest)
            {
                // Banana Cake Pop playground:
                //   script-src  unsafe-eval  → AJV uses new Function() for schema validation
                //   style-src   unsafe-inline → Emotion CSS-in-JS inserts <style> tags at runtime
                //   font-src    https: data:  → may fetch fonts from CDN or embed as data URIs
                //   img-src     data: blob:   → icons/thumbnails as data/blob URIs
                //   connect-src ws: wss:      → GraphQL subscriptions over WebSocket
                csp = "default-src 'self'; frame-ancestors 'none'; form-action 'self'; " +
                      "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                      "style-src 'self' 'unsafe-inline'; " +
                      "font-src 'self' https: data:; " +
                      "img-src 'self' data: blob:; " +
                      "connect-src 'self' ws: wss:;";
            }
        }

        headers["Content-Security-Policy"] = csp;

        // Restrict browser feature access
        headers["Permissions-Policy"] =
            "geolocation=(), microphone=(), camera=(), payment=(), usb=()";

        // Cache control for API responses (no client-side caching of sensitive data)
        headers["Cache-Control"] = "no-store";
        headers["Pragma"] = "no-cache";

        // Remove server identification headers
        headers.Remove("Server");
        headers.Remove("X-Powered-By");
        headers.Remove("X-AspNet-Version");
        headers.Remove("X-AspNetMvc-Version");

        await next(context);
    }
}
