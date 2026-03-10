using System.ComponentModel.DataAnnotations;

namespace McpServer.Settings;

public class CSharpCodeSettings
{
    public const string Section = "CSharpCodeSettings";

    [Url]
    [Required]
    public required string SearchEndpoint { get; set; }

    public int TopK { get; set; }
}
