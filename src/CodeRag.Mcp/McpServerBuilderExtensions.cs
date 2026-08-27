using CodeRag.Mcp.Tools;

namespace Microsoft.Extensions.DependencyInjection;

public static class McpServerBuilderExtensions
{
    /// <summary>
    /// Registers the Code RAG MCP tools (project discovery and semantic code search) on top of
    /// whichever transport the host has configured (HTTP, stdio, ...). Tools call straight into
    /// the Application layer - no HTTP round-trip to this API's own REST endpoints.
    /// </summary>
    /// <param name="builder">MCP server builder to register the tools onto.</param>
    public static IMcpServerBuilder AddCodeRagTools(this IMcpServerBuilder builder)
    {
        builder.WithTools<ProjectTools>();
        builder.WithTools<CodeQueryTools>();
        return builder;
    }
}
