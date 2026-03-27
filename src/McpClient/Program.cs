using DotNetEnv;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Client;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;

Env.TraversePath().Load();

var builder = new ConfigurationBuilder();
builder.SetBasePath(Directory.GetCurrentDirectory())
    .AddEnvironmentVariables();
var configuration = builder.Build();

var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

var clientId = Environment.GetEnvironmentVariable("MCP_CLIENT_ID");
var redirectUriString = Environment.GetEnvironmentVariable("MCP_REDIRECT_URI");
var scopesString = Environment.GetEnvironmentVariable("MCP_SCOPES");
var serverUrl = Environment.GetEnvironmentVariable("MCP_SERVER_URL");
var scopes = scopesString!
    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var cfg = new McpConfig
{
    ClientId = clientId!,
    RedirectUrl = redirectUriString!,
    Scopes = scopes,
    ServerUrl = serverUrl!
};

await ListTools(cfg);
await Call_Echo_Tool_RequiresAuth(cfg, cancellationToken);
await Connect_AnonymousAccess_ThrowsException(cfg);

Console.ReadKey();

static async Task Call_Echo_Tool_RequiresAuth(McpConfig cfg, CancellationToken token)
{
    var client = await Connect_RequiresAuth(cfg);
    var result = await client.CallToolAsync(
        "echo",
        new Dictionary<string, object?> { ["input"] = "Hello, MCP Server." },
        cancellationToken: token);

    var content = result.Content[0];
    var textResult = content.ToString();

    Console.WriteLine($"Echo tool result: {textResult}");
}

static async Task ListTools(McpConfig cfg)
{
    var client = await Connect_RequiresAuth(cfg);
    var tools = await client.ListToolsAsync();

    foreach (var tool in tools)
    {
        Console.WriteLine($"{tool.Name} ({tool.Description})");
        Console.WriteLine();
    }
}

static async Task Connect_AnonymousAccess_ThrowsException(McpConfig cfg)
{
    try
    {
        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(cfg.ServerUrl!),
            Name = "MCP HTTP Transport",
        };
        var clientTransport = new HttpClientTransport(transportOptions);
        var client = await McpClient.CreateAsync(clientTransport);
    }
    catch (HttpRequestException ex)
    {
        Debug.Assert(ex.StatusCode == HttpStatusCode.Unauthorized);
    }
}

static async Task<McpClient> Connect_RequiresAuth(McpConfig cfg)
{
    var transportOptions = new HttpClientTransportOptions
    {
        Endpoint = new Uri(cfg.ServerUrl!),
        Name = "MCP HTTP Transport",
        OAuth = new ClientOAuthOptions
        {
            RedirectUri = new Uri(cfg.RedirectUrl),
            AuthorizationRedirectDelegate = HandleAuthorizationUrlAsync,
            Scopes = cfg.Scopes,
            ClientId = cfg.ClientId
        }
    };
    var clientTransport = new HttpClientTransport(transportOptions);
    return await McpClient.CreateAsync(clientTransport);
}

static async Task<string?> HandleAuthorizationUrlAsync(
    Uri authorizationUrl,
    Uri redirectUri,
    CancellationToken cancellationToken)
{
    Console.WriteLine("Starting OAuth authorization flow...");
    Console.WriteLine($"Opening browser to: {authorizationUrl}");

    var listenerPrefix = $"{redirectUri.Scheme}://{redirectUri.Host}:{redirectUri.Port}/";
    if (!listenerPrefix.EndsWith("/")) listenerPrefix += "/";

    using var listener = new HttpListener();
    listener.Prefixes.Add(listenerPrefix);

    try
    {
        listener.Start();
        Console.WriteLine($"Listening for OAuth callback on: {listenerPrefix}");

        OpenBrowser(authorizationUrl);

        using var registration = cancellationToken.Register(() =>
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
        });

        var context = await listener.GetContextAsync();
        var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? string.Empty);
        var code = query["code"];
        var error = query["error"];

        const string responseHtml =
            "<html><body><h1>Authentication complete</h1><p>You can close this window now.</p></body></html>";

        byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
        context.Response.ContentLength64 = buffer.Length;
        context.Response.ContentType = "text/html";
        await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
        context.Response.Close();

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"Auth error: {error}");
            return null;
        }

        if (string.IsNullOrEmpty(code))
        {
            Console.WriteLine("No authorization code received.");
            return null;
        }

        Console.WriteLine("Authorization code received successfully.");
        return code;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error getting auth code: {ex.Message}");
        return null;
    }
    finally
    {
        if (listener.IsListening) listener.Stop();
    }
}

static void OpenBrowser(Uri url)
{
    if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
    {
        Console.WriteLine($"Error: Only HTTP and HTTPS URLs are allowed.");
        return;
    }

    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = url.ToString(),
            UseShellExecute = true
        };
        Process.Start(psi);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error opening browser: {ex.Message}");
        Console.WriteLine($"Please manually open this URL: {url}");
    }
}

readonly struct McpConfig
{
    public required string ClientId { get; init; }
    public required string RedirectUrl { get; init; }
    public required string[] Scopes { get; init; }
    public required string ServerUrl { get; init; }
}
