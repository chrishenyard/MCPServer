using McpServer;
using McpServer.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel(builder =>
{
    builder.AddServerHeader = false;
});

builder.Host
    .UseDefaultServiceProvider((context, options) =>
    {
        options.ValidateOnBuild = true;
    });

builder
    .AddConfiguration();

var configuration = builder.Configuration;

builder.Services
    .AddTelemetry(configuration)
    .AddExceptionHandler<GlobalExceptionHandler>()
    .AddProblemDetails()
    .AddHttp(configuration)
    .AddServices();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseExceptionHandler();
app.MapMcp();

await app.RunAsync();

