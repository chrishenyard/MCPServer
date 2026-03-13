namespace McpServer.Settings;

public class SeqSettings
{
    public const string Section = "SeqSettings";

    public string ServerUrl { get; set; } = null!;

    public string ApiKey { get; set; } = null!;
}
