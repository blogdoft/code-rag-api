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

                // 192.0.2.0/24 is reserved for documentation/testing (RFC 5737) and never
                // routable - unlike localhost:11434, this can't collide with a real Ollama
                // instance the test host or CI runner happens to have running.
                ["Embeddings:BaseUrl"] = "http://192.0.2.1:11434",
                ["Embeddings:Dimensions"] = "3",
                ["Embeddings:TimeoutSeconds"] = "2",
            });
        });
    }
}
