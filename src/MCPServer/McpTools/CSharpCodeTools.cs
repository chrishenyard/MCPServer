using McpServer.Models;
using McpServer.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace McpServer.McpTools;

[McpServerToolType]
public static class CSharpCodeTools
{
    [McpServerTool, Description("Find C# code from a local codebase.")]
    public static async Task<string> GetMonkey(CSharpCodeService codeService, [Description("The user's request")] AskRequest request)
    {
        var askRespone = await codeService.Ask(request);
        return JsonSerializer.Serialize(askRespone);
    }
}
