using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CodeRag.Api.Tests;

public sealed class CustomWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = connectionString,
                ["Embeddings:Provider"] = "Ollama",
                ["Embeddings:Model"] = "bge-m3",
                ["Embeddings:BaseUrl"] = "http://localhost:11434",
                ["Embeddings:Dimensions"] = "3",
            });
        });
    }
}
