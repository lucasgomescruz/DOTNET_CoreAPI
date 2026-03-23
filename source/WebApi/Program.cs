
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Project.Infrastructure.Data;
using Project.WebApi.Configurations;
using Project.WebApi.Infrastructure;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Microsoft.AspNetCore.DataProtection;

static async Task InitialiseDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

var builder = WebApplication.CreateBuilder(args);

// ── Configure DataProtection for container environments ───────────────────
var dataProtectionKeyPath = "/data/dataprotection-keys";
if (!Directory.Exists(dataProtectionKeyPath))
{
    Directory.CreateDirectory(dataProtectionKeyPath);
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath))
    .SetApplicationName("CoreAPI");

builder.Host.UseSerilog((ctx, services, logConfig) =>
{
    logConfig
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("app", "webapi")
        .Enrich.WithProperty("env", ctx.HostingEnvironment.EnvironmentName);

    var lokiUrl = ctx.Configuration["Loki:Url"];
    if (!string.IsNullOrWhiteSpace(lokiUrl))
    {
        logConfig.WriteTo.GrafanaLoki(
            lokiUrl,
            labels: new[]
            {
                new LokiLabel { Key = "app",  Value = "webapi" },
                new LokiLabel { Key = "env",  Value = ctx.HostingEnvironment.EnvironmentName }
            },
            propertiesAsLabels: new[] { "level" });
    }
});

// Remove the "Server" header at the Kestrel level
builder.WebHost.ConfigureKestrel(opt => opt.AddServerHeader = false);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddWebServices(builder.Configuration);
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await InitialiseDatabaseAsync(app);
}
else if (app.Environment.IsProduction())
{
    app.UseHsts();
    // Only redirect HTTP to HTTPS in production
    app.UseHttpsRedirection();
}

// Security headers — must be added before any response-producing middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var result = new
        {
            status = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "OK" : "DOWN",
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }).ToArray()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(result));
    }
});

app.UseCors("DefaultCors");

app.UseRateLimiter();

app.UseRouting();
// Prometheus metrics endpoints — protect /metrics with an API key
app.UseHttpMetrics(); // still record HTTP metrics for all routes

var metricsApiKey = app.Configuration["METRICS_API_KEY"];

app.UseWhen(context => context.Request.Path.StartsWithSegments("/metrics"), appBuilder =>
{
    appBuilder.Use(async (context, next) =>
    {
        var expected = metricsApiKey;
        if (string.IsNullOrEmpty(expected))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Metrics access forbidden: API key not configured");
            return;
        }

        string? provided = context.Request.Headers["X-API-KEY"].FirstOrDefault() ?? context.Request.Query["api_key"].FirstOrDefault();

        if (!string.Equals(provided, expected, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await next();
    });
});

app.UseMetricServer();
// Enable Swagger UI before authentication/authorization so it's accessible
app.UseSwaggerConfiguration();

app.UseAuthentication();

app.UseAuthorization();

// Request/Response logging (captures request, response and timing for all routes)
app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.MapControllers();

app.UseExceptionHandler(options => { });

app.Map("/", () => Results.Redirect("/swagger"));

app.Run();

public partial class Program { }

