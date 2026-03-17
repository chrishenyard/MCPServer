using McpServer.McpTools;
using McpServer.Services;
using McpServer.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
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
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        services.AddMcpServer()
            .WithHttpTransport()
            .AddAuthorizationFilters()
            .WithTools<CSharpCodeTools>();

        services
            .AddSerilog()
            .AddSingleton<CSharpCodeService>();

        return services;
    }

    public static IServiceCollection AddAuth(
        this IServiceCollection services,
        IConfiguration config)
    {
        var keycloakSettings = config
            .GetSection(KeycloakSettings.Section)
            .Get<KeycloakSettings>()!;

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakSettings.Authority;
                options.Audience = keycloakSettings.Audience;
                options.RequireHttpsMetadata = keycloakSettings.RequireHttpsMetadata;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        IConfiguration config)
    {
        var seqSettings = config
            .GetSection("SeqSettings")
            .Get<SeqSettings>()!;

        services.AddOpenTelemetry()
            .WithTracing(b => b.AddSource("*")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(seqSettings.ServerUrl);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
                }))
            .WithMetrics(b => b.AddMeter("*")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithLogging(logging => logging
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(seqSettings.ServerUrl);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
                }))
            .UseOtlpExporter();

        return services;
    }
}
