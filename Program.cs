using System.Collections.Frozen;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RoslynMcp.Mcp;
using Tool = ModelContextProtocol.Protocol.Tool;

var toolsList = ToolCatalog.Build();

var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "RoslynMcp", Version = "0.5.0" },
    ProtocolVersion = "2024-11-05",
    Capabilities = new ServerCapabilities { Tools = new ToolsCapability { ListChanged = false } },
    Handlers = new McpServerHandlers
    {
        ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult { Tools = toolsList }),

        CallToolHandler = async (request, cancellationToken) =>
        {
            var name = request.Params?.Name ?? "";
            var args = request.Params?.Arguments is IReadOnlyDictionary<string, JsonElement> a
                ? a
                : FrozenDictionary<string, JsonElement>.Empty;
            try
            {
                var text = await ToolHandlers.HandleAsync(name, args, cancellationToken).ConfigureAwait(false);
                var isError = text.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);
                return new CallToolResult { Content = [new TextContentBlock { Text = text }], IsError = isError };
            }
            catch (ArgumentException ex)
            {
                return new CallToolResult { Content = [new TextContentBlock { Text = $"Error: {ex.Message}" }], IsError = true };
            }
            catch (Exception ex)
            {
                return new CallToolResult { Content = [new TextContentBlock { Text = $"Error: {ex.Message}" }], IsError = true };
            }
        }
    }
};

var transport = new StdioServerTransport("RoslynMcp");
await using var server = McpServer.Create(transport, options);
await server.RunAsync();
return 0;
