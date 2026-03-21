using McpServer.McpTools;
using McpServer.Services;
using McpServer.Settings;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
        .AddStandardResilienceHandler();

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
            .AddSingleton<CSharpCodeService>();

        return services;
    }

    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        IConfiguration config)
    {
        var seqSettings = config.GetSection("SeqSettings")
            .Get<SeqSettings>()!;
        var baseUri = new Uri(seqSettings.ServerUrl);
        var app = services.BuildServiceProvider();
        var hostEnvironment = app.GetRequiredService<IHostEnvironment>();
        var environment = hostEnvironment.IsDevelopment() ? "development" : "production";

        services.AddLogging(logging => logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(
                ResourceBuilder.CreateEmpty()
                    .AddService("McpServer")
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = environment
                    }));

            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;

            options.AddOtlpExporter(exporter =>
            {
                exporter.Endpoint = new Uri(baseUri, "/ingest/otlp/v1/logs");
                exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
                exporter.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
            });

            options.AddConsoleExporter();
        }));

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("McpServer"))
            .WithMetrics(metrics => metrics
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(baseUri, "/ingest/otlp/v1/metrics");
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
                }))
            .WithTracing(tracing => tracing
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(baseUri, "/ingest/otlp/v1/traces");
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
                }));

        return services;
    }
}
