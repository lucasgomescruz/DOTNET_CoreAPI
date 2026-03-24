using Project.Application.Common.Interfaces;
using Project.Infrastructure.Data;
using Project.WebApi.Services;
using Microsoft.AspNetCore.Mvc;
using Project.WebApi.Configurations;
using System.Globalization;
using Project.Domain.Notifications;
using Project.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Project.Domain.Constants;
using System.Threading.RateLimiting;
using Project.Application.Common.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>();

        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings!.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                RoleClaimType = ClaimTypes.Role,
                ClockSkew = TimeSpan.Zero
            };
        });

        // ── CORS ───────────────────────────────
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy("DefaultCors", policy =>
            {
                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                else
                    policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
            });
        });

        // ── Rate Limiting ───────────────────────
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // General API: 200 req / min per IP
            options.AddPolicy("global", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit          = 200,
                        Window               = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit           = 0
                    }));

            // Auth endpoints: 10 req / min per IP (brute-force protection)
            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit          = 10,
                        Window               = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit           = 0
                    }));
        });

        services.AddScoped<IUser, CurrentUser>();

        services.AddNotifications();

        services.AddHttpContextAccessor();

        services.AddControllers();

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(name: "database")
            .AddCheck<Project.WebApi.Infrastructure.RedisHealthCheck>("redis")
            .AddCheck<Project.WebApi.Infrastructure.RabbitMqHealthCheck>("rabbitmq");

        // Run health checks periodically and export per-dependency Prometheus metrics
        services.AddHostedService<Project.WebApi.Infrastructure.HealthCheckMetricsPublisher>();

        services.AddExceptionHandler<CustomExceptionHandler>();

        services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);

        services.AddSwaggerConfiguration();

        // ── GraphQL (Hot Chocolate) ────────────────────────────────────────────
        services
            .AddGraphQLServer()
            .AddQueryType<Project.WebApi.GraphQL.AuthQuery>()
            .AddMutationType<Project.WebApi.GraphQL.AuthMutation>()
            .AddAuthorization()
            .AddHttpRequestInterceptor<Project.WebApi.GraphQL.CultureHttpRequestInterceptor>()
            .ModifyRequestOptions(opt =>
            {
                // Expose full exception details in Development so errors are never
                // hidden as "Unexpected Execution Error".
                opt.IncludeExceptionDetails =
                    string.Equals(
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                        "Development",
                        StringComparison.OrdinalIgnoreCase);
            });

        return services;
    }
    private static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        // Register the concrete handlers as themselves first (scoped).
        // MediatR resolves INotificationHandler<T> and the GraphQL resolvers
        // both need to share the SAME scoped instance so notifications published
        // during a command handler are visible when the resolver checks them.
        services.AddScoped<DomainNotificationHandler>();
        services.AddScoped<DomainSuccessNotificationHandler>();

        // Forward the interface registrations to the already-registered concrete instances.
        services.AddScoped<INotificationHandler<DomainNotification>>(sp => sp.GetRequiredService<DomainNotificationHandler>());
        services.AddScoped<INotificationHandler<DomainSuccessNotification>>(sp => sp.GetRequiredService<DomainSuccessNotificationHandler>());

        return services;
    }
}
