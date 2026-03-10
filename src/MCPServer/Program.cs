using MCPServer.Configuration;

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

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseExceptionHandler();

await app.RunAsync();

