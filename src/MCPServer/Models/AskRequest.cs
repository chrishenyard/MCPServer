using System.Text.Json.Serialization;

namespace McpServer.Models;

public class AskRequest
{
    public AskRequest() { }

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("topK")]
    public int TopK { get; set; } = 10;
}
