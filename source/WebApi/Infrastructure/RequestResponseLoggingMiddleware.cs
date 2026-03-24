using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
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
        string? maskedRequestBody = null;

        try
        {
            if (request.ContentLength > 0 && request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                request.EnableBuffering();
                using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                request.Body.Position = 0;
                maskedRequestBody = MaskSensitiveData(requestBody);

                // Pretty-print GraphQL requests for readability
                if (request.Path.StartsWithSegments("/graphql") && !string.IsNullOrWhiteSpace(maskedRequestBody))
                {
                    try
                    {
                        var parsed = System.Text.Json.JsonDocument.Parse(maskedRequestBody);
                        maskedRequestBody = System.Text.Json.JsonSerializer.Serialize(parsed.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        
                        // Normalize escaped newlines in query strings for better readability
                        maskedRequestBody = NormalizeStringLiterals(maskedRequestBody);
                    }
                    catch
                    {
                        // If formatting fails, use the original (unformatted) request
                    }
                }
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

        _logger.LogInformation("➤ Incoming request {method} {path}\n  QueryString: {qs}\n  User: {username} ({userId})\n  Body:\n{body}",
            request.Method, request.Path, request.QueryString, username ?? "Anonymous", userId ?? "N/A", 
            maskedRequestBody != null ? "    " + string.Join("\n    ", maskedRequestBody.Split('\n')) : "    [empty]");

        // Always intercept the response stream so we can inspect the content-type
        // before deciding whether to log the body.
        // - JSON / text responses (REST + GraphQL queries): body is decoded and logged.
        // - Binary/non-text responses (playground HTML/CSS/JS assets, etc.): body is
        //   NOT decoded — it is copied back untouched and logged as "[non-text]".
        var originalBodyStream = context.Response.Body;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            sw.Stop();

            // Inspect content-type AFTER the handler ran (it is set by the handler).
            var contentType = context.Response.ContentType ?? string.Empty;
            bool isTextResponse = contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                               || contentType.Contains("application/graphql-response", StringComparison.OrdinalIgnoreCase)
                               || contentType.Contains("text/", StringComparison.OrdinalIgnoreCase)
                               || (request.Path.StartsWithSegments("/graphql") && contentType == string.Empty);

            // Diagnostic: record reported Content-Type and response byte length
            var responseLength = responseBody.Length;
            _logger.LogDebug("Response content-type={contentType} length={length} bytes for {method} {path}", contentType, responseLength, request.Method, request.Path);

            string? responseText = null;
            if (isTextResponse && responseLength > 0)
            {
                context.Response.Body.Seek(0, SeekOrigin.Begin);
                responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
                context.Response.Body.Seek(0, SeekOrigin.Begin);

                // For GraphQL responses, attempt to pretty-print the JSON for readability
                if (request.Path.StartsWithSegments("/graphql") && !string.IsNullOrWhiteSpace(responseText))
                {
                    try
                    {
                        var parsed = System.Text.Json.JsonDocument.Parse(responseText);
                        responseText = System.Text.Json.JsonSerializer.Serialize(parsed.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        
                        // Normalize escaped newlines and other characters for better readability
                        responseText = NormalizeStringLiterals(responseText);
                    }
                    catch
                    {
                        // If formatting fails, use the original (unformatted) response
                    }
                }
            }

            _logger.LogInformation("⬅ Outgoing response {statusCode} for {method} {path} [{duration}ms]\n  User: {username} ({userId})\n  Body:\n{response}",
                context.Response.StatusCode, request.Method, request.Path, sw.ElapsedMilliseconds, username ?? "Anonymous", userId ?? "N/A",
                responseText != null ? "    " + string.Join("\n    ", responseText.Split('\n')) : "    [empty]");

            // Copy the intercepted response back to the original stream so the client receives it
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "✗ Unhandled exception for {method} {path} [{duration}ms] UserId={userId}", request.Method, request.Path, sw.ElapsedMilliseconds, userId);
            throw;
        }
        finally
        {
            // Ensure the response stream is restored for downstream middleware and connection cleanup
            context.Response.Body = originalBodyStream;
            responseBody.Dispose();
        }
    }

    private static string? MaskSensitiveData(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return body;

        try
        {
            // Mask JSON string values for common password keys (case-insensitive)
            var sensitiveKeys = "password|pwd|pass|senha";
            var jsonPattern = new Regex($"(?<pre>\"(?i:{sensitiveKeys})\"\\s*:\\s*\")(?<val>.*?)(?<post>\")", RegexOptions.Singleline);
            var masked = jsonPattern.Replace(body, "${pre}*****${post}");

            // Also mask form-encoded bodies like password=...&
            var formPattern = new Regex("(?i)\b(password|pwd|pass|senha)=([^&\\r\\n]+)");
            masked = formPattern.Replace(masked, m => $"{m.Groups[1].Value}=*****");

            return masked;
        }
        catch
        {
            return body;
        }
    }

    private static string NormalizeStringLiterals(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        try
        {
            // Replace escaped newlines and carriage returns with actual formatting breaks
            // This makes GraphQL query strings and error messages much more readable in logs
            json = json.Replace("\\n", "\n");
            json = json.Replace("\\r", "");
            json = json.Replace("\\t", "\t");
            
            // Remove excessive blank lines (collapse multiple blank lines into a single one)
            json = Regex.Replace(json, @"\n\s*\n(\s*\n)+", "\n\n");
            
            // Trim leading/trailing whitespace from each line within string values
            var lines = json.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                // Only trim trailing spaces, preserve indentation for JSON structure
                lines[i] = lines[i].TrimEnd();
            }
            json = string.Join("\n", lines);
            
            return json;
        }
        catch
        {
            return json;
        }
    }
}
