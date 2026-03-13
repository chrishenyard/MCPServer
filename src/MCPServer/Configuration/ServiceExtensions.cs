using McpServer.McpTools;
using McpServer.Services;
using McpServer.Settings;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Serilog;

namespace McpServer.Configuration;

public static class ServiceExtensions
{
    public static IServiceCollection AddHttp(
        this IServiceCollection services,
        IConfiguration config)
    {
        var settings = config
            .GetSection(CSharpCodeSettings.Section)
            .Get<CSharpCodeSettings>()!;

        services.AddHttpClient("CSharpCodeService", client =>
        {
            client.BaseAddress = new Uri(settings.SearchEndpoint);
        })
        .AddResilienceHandler("custom-pipeline", builder =>
        {
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential
            })
            .AddTimeout(new HttpTimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(5) // Max duration for each attempt
            });
        });

        return services;
    }

    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services
           .AddOptions<CSharpCodeSettings>();

        return services;
    }

    public static WebApplicationBuilder AddConfiguration(this WebApplicationBuilder builder)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        builder.Configuration.AddConfiguration(configuration);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
            .WriteTo.File(
                "logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {CorrelationId} {Level:u3}] {Username} {Message:lj}{NewLine}{Exception}")
            .Enrich.FromLogContext()
            .CreateLogger();

        return builder;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithHttpTransport()
            .WithTools<CSharpCodeTools>();

        services
            .AddSerilog()
            .AddSingleton<CSharpCodeService>();

        return services;
    }
}
