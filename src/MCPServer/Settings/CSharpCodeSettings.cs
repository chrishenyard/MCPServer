namespace McpServer.Settings;

public class CSharpCodeSettings
{
    public const string Section = "CSharpCodeSettings";

    public string SearchEndpoint { get; set; } = null!;

    public int TopK { get; set; }
}
