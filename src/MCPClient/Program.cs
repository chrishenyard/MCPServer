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

var clientId = Environment.GetEnvironmentVariable("MCP_CLIENT_ID");
var clientSecret = Environment.GetEnvironmentVariable("MCP_CLIENT_SECRET");
var redirectUriString = Environment.GetEnvironmentVariable("MCP_REDIRECT_URI");
var scopesString = Environment.GetEnvironmentVariable("MCP_SCOPES");
var serverUrl = Environment.GetEnvironmentVariable("MCP_SERVER_URL");
var scopes = scopesString!
    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var transportOptions = new HttpClientTransportOptions
{
    Endpoint = new Uri(serverUrl!),
    Name = "MCP HTTP Transport",
    OAuth = new ClientOAuthOptions
    {
        RedirectUri = new Uri(redirectUriString!),
        AuthorizationRedirectDelegate = HandleAuthorizationUrlAsync,
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
static async Task<string?> HandleAuthorizationUrlAsync(Uri authorizationUrl, Uri redirectUri, CancellationToken cancellationToken)
{
    Console.WriteLine("Starting OAuth authorization flow...");
    Console.WriteLine($"Opening browser to: {authorizationUrl}");

    //var listenerPrefix = redirectUri.GetLeftPart(UriPartial.Authority);
    var listenerPrefix = redirectUri.Scheme + "://" + redirectUri.Host + ":9000/";
    if (!listenerPrefix.EndsWith("/")) listenerPrefix += "/";

    using var listener = new HttpListener();
    listener.Prefixes.Add(listenerPrefix);

    try
    {
        listener.Start();
        Console.WriteLine($"Listening for OAuth callback on: {listenerPrefix}");

        OpenBrowser(authorizationUrl);

        var context = await listener.GetContextAsync();
        var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? string.Empty);
        var code = query["code"];
        var error = query["error"];

        string responseHtml = "<html><body><h1>Authentication complete</h1><p>You can close this window now.</p></body></html>";
        byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
        context.Response.ContentLength64 = buffer.Length;
        context.Response.ContentType = "text/html";
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.Close();

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"Auth error: {error}");
            return null;
        }

        if (string.IsNullOrEmpty(code))
        {
            Console.WriteLine("No authorization code received");
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



