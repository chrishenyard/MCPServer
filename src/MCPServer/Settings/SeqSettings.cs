using System.ComponentModel.DataAnnotations;

namespace MCPServer.Settings;

public class SeqSettings
{
    public const string Section = "SeqSettings";

    [Url]
    [Required]
    public required string ServerUrl { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public required string ApiKey { get; set; }
}
