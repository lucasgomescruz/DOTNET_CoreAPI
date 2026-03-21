using Project.Application.Common.Interfaces;
using Project.Application.Common.Models;
using Project.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Project.Infrastructure.Data.Respositories;
using Project.Domain.Interfaces.Data.Repositories;
using StackExchange.Redis;
using RabbitMQ.Client;
using Project.Infrastructure.Authentication;
using Project.Domain.Interfaces.Services;
using Project.Infrastructure.Email;
using Project.Infrastructure.Cache;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRepositories();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        Guard.Against.Null(connectionString, message: "Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());


            options.UseNpgsql(connectionString);

        });

        var redisConnectionString = configuration.GetSection("Redis")["Connection"];
        if (string.IsNullOrEmpty(redisConnectionString))
        {
            throw new InvalidOperationException("Redis connection string is not configured.");
        }
        
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect($"{redisConnectionString},abortConnect=false"));

        var rabbitMqConnectionString = configuration.GetSection("RabbitMQ")["Connection"];
        if (string.IsNullOrEmpty(rabbitMqConnectionString))
        {
            throw new InvalidOperationException("RabbitMQ connection string is not configured.");
        }

        // Register as typed IConnection so consumers can inject it by interface
        services.AddSingleton<IConnection>(sp =>
        {
            var factory = new ConnectionFactory()
            {
                Uri = new Uri(rabbitMqConnectionString),
                DispatchConsumersAsync = true
            };

            return factory.CreateConnection();
        });

        services.Configure<RabbitMQSettings>(configuration.GetSection("RabbitMQ"));
        services.AddSingleton<IEmailQueuePublisher, EmailQueuePublisher>();
        services.AddHostedService<EmailConsumerService>();

        services.Configure<EmailSettings>(configuration.GetSection("Email"));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ApplicationDbContextInitialiser>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IRedisService, RedisService>();

        services.AddAuthorizationBuilder();

        services.AddSingleton(TimeProvider.System);

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepositoryBase<>), typeof(RepositoryBase<>));

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.Name.EndsWith("Repository") && t.IsClass && !t.IsAbstract)
                .ToList();

            foreach (var implementationType in types)
            {
                var interfaceType = implementationType.GetInterface($"I{implementationType.Name}");

                if (interfaceType != null)
                {
                    services.AddTransient(interfaceType, implementationType);
                }
            }
        }

        return services;
    }

}
