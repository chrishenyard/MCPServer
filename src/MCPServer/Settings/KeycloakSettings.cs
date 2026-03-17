namespace McpServer.Settings;

public sealed class KeycloakSettings
{
    public const string Section = "KeycloakSettings";

    public string Authority { get; set; } = string.Empty;

    public string? Audience { get; set; }

    // This must be the externally reachable MCP server URL.
    // Example: http://localhost:5000
    public string ServerUrl { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; }

    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
}