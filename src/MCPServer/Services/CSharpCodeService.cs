using McpServer.McpTools;
using McpServer.Models;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace McpServer.Services;

public class CSharpCodeService(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("CSharpCodeService");

    public async Task<AskResponse> SearchLocalRepo(string prompt, int topK = 10)
    {
        var req = new
        {
            Question = prompt,
            TopK = topK
        };
        var askResponse = new AskResponse(string.Empty, []);
        var todoItemJson = new StringContent(
        JsonSerializer.Serialize(req, AppJsonSerializerContext.Default.String),
        Encoding.UTF8,
        Application.Json);

        var response = await _httpClient.PostAsync("/api/ask", todoItemJson);
        if (response.IsSuccessStatusCode)
        {
            var responseStream = await response.Content.ReadAsStreamAsync();
            askResponse = await JsonSerializer.DeserializeAsync(responseStream, AppJsonSerializerContext.Default.AskResponse);
        }

        askResponse ??= new AskResponse(string.Empty, []);

        return askResponse;
    }
}
