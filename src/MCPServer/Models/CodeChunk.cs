namespace McpServer.Models;

public sealed class CodeChunk
{
    public Guid Id { get; set; }

    public string Filename { get; set; } = "";

    public string Language { get; set; } = "";

    public string Content { get; set; } = "";

    public string Hash { get; set; } = "";

    public ReadOnlyMemory<float>? Embedding { get; set; }
}
