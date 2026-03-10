using McpServer.Services;
using McpServer.Settings;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;

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
           .AddOptions<CSharpCodeSettings>()
           .BindConfiguration(CSharpCodeSettings.Section)
           .ValidateDataAnnotations()
           .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        IConfiguration config)
    {
        var seqSettings = config.GetSection("SeqSettings")
            .Get<SeqSettings>()!;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("MCPSearch")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(seqSettings.ServerUrl);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    options.EnrichWithIDbCommand = (activity, command) =>
                    {
                        activity.SetTag("db.statement", command.CommandText);
                    };
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.FilterHttpRequestMessage = (httpRequestMessage) =>
                    {
                        // Ensure Ollama requests are captured
                        return true;
                    };
                })
                .AddSource("OllamaSharp")
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(seqSettings.ServerUrl);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
                }))
            .WithLogging(logging => logging
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(seqSettings.ServerUrl);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
                }));

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

        return builder;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

        services.AddSingleton<CSharpCodeService>();

        return services;
    }
}
