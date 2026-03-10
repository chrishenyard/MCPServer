using McpServer.Models;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace McpServer.Services;

public class CSharpCodeService(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("CSharpCodeService");

    public async Task<AskResponse> Ask(AskRequest req)
    {
        var askResponse = new AskResponse(string.Empty, []);
        var todoItemJson = new StringContent(
        JsonSerializer.Serialize(req),
        Encoding.UTF8,
        Application.Json);

        var response = await _httpClient.PostAsync("/api/ask", todoItemJson);
        if (response.IsSuccessStatusCode)
        {
            var responseStream = await response.Content.ReadAsStreamAsync();
            askResponse = await JsonSerializer.DeserializeAsync<AskResponse>(responseStream);
        }

        askResponse ??= new AskResponse(string.Empty, []);

        return askResponse;
    }
}
