using McpServer.Models;
using McpServer.Services;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpServer.McpTools;

[McpServerToolType]
[Authorize(Roles = "mcp:tools")]
public class CSharpCodeTools
{
    /*
    TODO: The JSON serializer does not support deserializing to record types with parameterized constructors, which is what AskRequest is.

    When using a AskRquest object, a runtime exception will be thrown.

    Unhandled exception. System.NotSupportedException: JsonTypeInfo metadata for type 'McpServer.Models.AskRequest' 
    was not provided by TypeInfoResolver of type '[ModelContextProtocol.McpJsonUtilities+JsonContext, 
    System.Text.Json.Serialization.Metadata.JsonTypeInfoResolverWithAddedModifiers]'.

     */
    [McpServerTool(Name = "search_local_repo", Title = "Search Local Repo")]
    [Description("Use this tool when a user wants to perform a semantic code search of a private code repo. " +
        "Returns a serialized JSON AskResponse object which includes a LLM answer to the question and a list of " +
        "matching code snippets.")]
    public static async Task<string> SearchLocalRepo(
        CSharpCodeService codeService,
        [Description("The user's prompt")] string prompt,
        [Description("Maximum number of results to return")] int topK = 10,
        CancellationToken token = default)
    {
        var askRequest = new AskRequest
        {
            Question = prompt,
            TopK = topK
        };

        var askResponse = await codeService.SearchLocalRepo(askRequest, token);
        return JsonSerializer.Serialize(askResponse, AppJsonSerializerContext.Default.AskResponse);
    }

    [McpServerTool(Name = "echo", Title = "Echo")]
    [Description("A simple tool that echoes back the input. Useful for testing connectivity and latency.")]
    public static async Task<string> Echo(
        [Description("The input to echo back")] string input)
    {
        await Task.Yield();
        return input;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(CodeChunk))]
[JsonSerializable(typeof(AskRequest))]
[JsonSerializable(typeof(AskResponse))]
[JsonSerializable(typeof(SearchResonse))]
[JsonSerializable(typeof(List<SearchResonse>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }