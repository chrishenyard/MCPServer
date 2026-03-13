using McpServer.McpTools;
using McpServer.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace McpServer.Services;

public class CSharpCodeService(
    IHttpClientFactory httpClientFactory,
    ILogger<CSharpCodeService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("CSharpCodeService");
    private readonly ILogger<CSharpCodeService> _logger = logger;

    public async Task<AskResponse> SearchLocalRepo(AskRequest askRequest, CancellationToken token)
    {
        try
        {
            var askResponse = new AskResponse(string.Empty, []);
            var requestJson = new StringContent(
                JsonSerializer.Serialize(askRequest, AppJsonSerializerContext.Default.AskRequest),
                Encoding.UTF8,
                Application.Json);

            var response = await _httpClient.PostAsync("/api/ask", requestJson, token);
            if (response.IsSuccessStatusCode)
            {
                var responseStream = await response.Content.ReadAsStreamAsync(token);
                askResponse = await JsonSerializer.DeserializeAsync(responseStream, AppJsonSerializerContext.Default.AskResponse, token);
            }

            return askResponse ?? new AskResponse(string.Empty, []);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Remote semantic search failed. Falling back to local scan.");
            return BuildLocalFallback(askRequest);
        }
    }

    private static AskResponse BuildLocalFallback(AskRequest askRequest)
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            return new AskResponse("Local fallback could not locate the repository root.", []);
        }

        var terms = askRequest.Question
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (terms.Length == 0)
        {
            terms = ["chunk", "chunks", "split", "snippet", "codechunk"];
        }

        var files = Directory
            .EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase));

        var results = new List<SearchResonse>();

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var score = terms.Count(term => content.Contains(term, StringComparison.OrdinalIgnoreCase));
            if (score == 0)
            {
                continue;
            }

            var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            var snippet = content.Length > 1200 ? content[..1200] : content;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

            results.Add(new SearchResonse(
                new CodeChunk
                {
                    Id = Guid.NewGuid(),
                    Filename = relative,
                    Language = "csharp",
                    Content = snippet,
                    Hash = hash
                },
                score));
        }

        var topK = askRequest.TopK <= 0 ? 10 : askRequest.TopK;
        var ordered = results
            .OrderByDescending(r => r.Score ?? 0)
            .ThenBy(r => r.Chunk.Filename, StringComparer.OrdinalIgnoreCase)
            .Take(topK)
            .ToList();

        var answer = ordered.Count == 0
            ? "No local C# matches were found for the query."
            : $"Local fallback found {ordered.Count} C# match(es).";

        return new AskResponse(answer, ordered);
    }

    private static string? FindRepoRoot()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        }
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var start in candidates)
        {
            var dir = new DirectoryInfo(start);
            for (var i = 0; i < 10 && dir is not null; i++)
            {
                var srcPath = Path.Combine(dir.FullName, "src", "McpServer");
                if (Directory.Exists(srcPath))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }
}
