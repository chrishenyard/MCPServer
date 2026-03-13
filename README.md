# McpServer

ASP.NET Core implementation of a [Model Context Protocol](https://github.com/modelcontextprotocol) (MCP) server that exposes tools for working with C# code, including a semantic `search_local_repo` tool backed by an external search service with a local fallback.

## Features

- HTTP-based MCP server (`MapMcp`) suitable for use from MCP-enabled clients.
- `search_local_repo` tool for semantic search over a C# repository.
  - Calls a configurable remote semantic search endpoint.
  - Falls back to a local scan of the repo when the remote call fails.
- Strongly typed request/response models (`AskRequest`, `AskResponse`, `CodeChunk`).
- Source-generated JSON serialization via `AppJsonSerializerContext`.
- Resilient HTTP client with retry and timeout policies.
- Centralized error handling via `GlobalExceptionHandler` and `ProblemDetails`.

## Project Structure

- `src/McpServer/Program.cs` – minimal hosting setup, wiring of services and MCP endpoint.
- `src/McpServer/Configuration/ServiceExtensions.cs` – DI and configuration extension methods:
  - `AddHttp` – configures the `CSharpCodeService` HTTP client and resilience.
  - `AddSettings` – binds `CSharpCodeSettings` from configuration.
  - `AddServices` – registers MCP server and tools.
- `src/McpServer/McpTools/CSharpCodeTools.cs` – MCP tool definitions, including `search_local_repo`.
- `src/McpServer/Services/CSharpCodeService.cs` – calls the external semantic search API and implements local fallback.
- `src/McpServer/Models/*.cs` – DTOs used by the tool and service:
  - `AskRequest` – question text and `TopK` value.
  - `AskResponse` / `SearchResonse` – answer and ranked code chunks.
  - `CodeChunk` – information about each code snippet.
- `src/McpServer/Settings/CSharpCodeSettings.cs` – configuration for the external search service.
- `src/McpServer/appsettings.json` – default configuration (logging and `CSharpCodeSettings`).
- `src/McpServer/McpServer.http` – example HTTP requests for manual testing.
- `.mcp.json` – example MCP client configuration pointing at this server.

## Requirements

- .NET SDK compatible with the target framework configured in the project (see `.csproj`).
- An external semantic search API that accepts the `AskRequest` payload and returns an `AskResponse`-compatible JSON body, or willingness to rely on the built-in local fallback.

## Configuration

Primary configuration lives in `src/McpServer/appsettings.json` and environment-specific overrides (for example `appsettings.Development.json`). Relevant section:

```json
"CSharpCodeSettings": {
  "SearchEndpoint": "http://localhost:9020/api/ask",
  "TopK": 10
}
```

- `SearchEndpoint` – URL of the remote semantic search endpoint the server will POST to.
- `TopK` – default maximum number of results for searches.

Configuration is loaded and attached to the builder in `AddConfiguration`, and bound to `CSharpCodeSettings` via `AddSettings`.

## Running the Server

From the repository root:

```bash
cd src/McpServer
 dotnet run
```

By default, `Program.cs` configures Kestrel to listen on:

- `http://*:5000`

You can adjust the URLs in `Program.cs` or via standard ASP.NET Core hosting configuration (for example the `ASPNETCORE_URLS` environment variable).

## MCP Integration

The server uses `AddMcpServer()` and `MapMcp()` to expose tools over HTTP. A minimal MCP HTTP client request flow is illustrated in `src/McpServer/McpServer.http`:

1. `initialize` call to start a session.
2. `tools/call` with `name: "search_local_repo"` to execute the tool.

Example `tools/call` body:

```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "search_local_repo",
    "arguments": {
      "prompt": "Find code that chunks c# files",
      "topK": 10
    }
  }
}
```

Adjust your client-side `.mcp.json` (or equivalent) to point at the URL where this server is running. The sample `.mcp.json` included in the repo shows a basic HTTP configuration.

## How `search_local_repo` Works

1. The MCP runtime invokes `CSharpCodeTools.SearchLocalRepo` with:
   - `prompt` – the query text.
   - `topK` – an optional maximum number of results.
2. The tool creates an `AskRequest` instance and calls `CSharpCodeService.SearchLocalRepo`.
3. `CSharpCodeService`:
   - Serializes the request with `AppJsonSerializerContext`.
   - POSTs it to the configured `SearchEndpoint`.
   - Deserializes the response to `AskResponse` on success.
   - If the HTTP call fails or throws, logs a warning and falls back to a local search.
4. The local fallback:
   - Locates the repo root by walking up from the current directory.
   - Enumerates `*.cs` files under `src/McpServer`, skipping `bin`/`obj`.
   - Scores files by how many query terms they contain.
   - Builds `CodeChunk` results with a short snippet and content hash.
   - Returns an `AskResponse` summarizing matches.
5. `CSharpCodeTools.SearchLocalRepo` serializes the `AskResponse` to JSON using `AppJsonSerializerContext` and returns it to the MCP client.

## JSON Serialization

The project uses source-generated serialization via `AppJsonSerializerContext` in `CSharpCodeTools.cs`, annotated with `JsonSerializable` for all relevant types (`AskRequest`, `AskResponse`, `SearchResonse`, `CodeChunk`, and some primitives). This context is passed explicitly to `JsonSerializer` to ensure consistent, efficient serialization for both HTTP calls and MCP responses.

## Extending the Server

To add a new MCP tool:

1. Create a new public class in `src/McpServer/McpTools` marked with `[McpServerToolType]`.
2. Add static methods annotated with `[McpServerTool]` (and optional `[Description]`).
3. Register the tool type in `AddServices` via `.WithTools<YourToolClass>()`.
4. If the tool needs new request/response models, add them under `src/McpServer/Models` and register them with `AppJsonSerializerContext` via additional `JsonSerializable` attributes.

After adding tools or models, rebuild and restart the server so the MCP client can discover the new capabilities.
