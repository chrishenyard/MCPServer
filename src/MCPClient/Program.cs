using DotNetEnv;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Client;

Env.TraversePath().Load();

var builder = new ConfigurationBuilder();
builder.SetBasePath(Directory.GetCurrentDirectory())
    .AddEnvironmentVariables();

var configuration = builder.Build();
var clientId = Environment.GetEnvironmentVariable("MCP_CLIENT_ID")
    ?? throw new InvalidOperationException("MCP_CLIENT_ID environment variable is required.");
var clientSecret = Environment.GetEnvironmentVariable("MCP_CLIENT_SECRET")
    ?? throw new InvalidOperationException("MCP_CLIENT_SECRET environment variable is required.");

var redirectUriString = Environment.GetEnvironmentVariable("MCP_REDIRECT_URI")
    ?? throw new InvalidOperationException("MCP_REDIRECT_URI environment variable is required.");

var scopesString = Environment.GetEnvironmentVariable("MCP_SCOPES")
    // Default includes 'openid', which Keycloak typically requires for OIDC
    ?? "openid profile email mcp:tools";

var scopes = scopesString
    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var transportOptions = new HttpClientTransportOptions
{
    Endpoint = new Uri("http://localhost:5000"),
    Name = "My MCP HTTP Transport",
    OAuth = new ClientOAuthOptions
    {
        RedirectUri = new Uri(redirectUriString),
        Scopes = scopes,
        ClientId = clientId,
        ClientSecret = clientSecret
    }
};

var clientTransport = new HttpClientTransport(transportOptions);
var mcpClient = await McpClient.CreateAsync(clientTransport);

foreach (var tool in await mcpClient.ListToolsAsync())
{
    Console.WriteLine($"{tool.Name} ({tool.Description})");
    Console.WriteLine();
}
Console.WriteLine();

