using McpServer.Models;
using McpServer.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpServer.McpTools;

[McpServerToolType]
public class CSharpCodeTools
{
    [McpServerTool(Name = "search_local_repo", Title = "Search Local Repo")]
    [Description("Use this tool when a user wants to perform a semantic code search of a private code repo. " +
        "Returns a serialized JSON AskResponse object which includes a LLM answer to the question and a list of " +
        "matching code snippets.")]
    public static async Task<string> SearchLocalRepo(CSharpCodeService codeService, [Description("The user's prompt")] string prompt, int topK = 10)
    {
        var askResponse = await codeService.SearchLocalRepo(prompt, topK);
        return JsonSerializer.Serialize(askResponse, AppJsonSerializerContext.Default.AskResponse);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AskResponse))]
[JsonSerializable(typeof(SearchResonse))]
[JsonSerializable(typeof(List<SearchResonse>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }