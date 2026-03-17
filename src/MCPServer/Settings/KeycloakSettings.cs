namespace McpServer.Settings;

public class KeycloakSettings
{
    public const string Section = "KeycloakSettings";

    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = true;
}
