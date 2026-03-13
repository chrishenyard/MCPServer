using McpServer.Models;
using McpServer.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace McpServer.McpTools;

[McpServerToolType]
public static class CSharpCodeTools
{
    [McpServerTool(Name = "search_local_repo", Title = "Search Local Repo")]
    [Description("Use this tool when a user wants to perform a semantic code search of a private code repo. " +
        "Returns a serialized JSON AskResponse object which includes a LLM answer to the question and a list of " +
        "matching code snippets.")]
    public static async Task<string> SearchLocalRepo(CSharpCodeService codeService, [Description("The user's request")] AskRequest request)
    {
        var askRespone = await codeService.SearchLocalRepo(request);
        return JsonSerializer.Serialize(askRespone);
    }
}
