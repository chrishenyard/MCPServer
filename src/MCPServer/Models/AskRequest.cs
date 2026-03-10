namespace McpServer.Models;

public record AskRequest(string Question, int TopK = 10);
