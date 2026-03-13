using McpServer;
using McpServer.Configuration;
using Serilog;

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
    .AddExceptionHandler<GlobalExceptionHandler>()
    .AddProblemDetails()
    .AddHttp(configuration)
    .AddServices();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.MapMcp();

await app.RunAsync();

