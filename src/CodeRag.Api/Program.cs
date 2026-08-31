using CodeRag.Api.Filters;
using CodeRag.Application;
using CodeRag.Embeddings.Abstraction;
using CodeRag.Embeddings.Local;
using CodeRag.Embeddings.Ollama;
using CodeRag.Embeddings.OpenAI;
using CodeRag.Infrastructure.Database;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .AddControllers(options =>
        {
            options.Filters.Add<UnhandledExceptionFilter>();

            // The API exclusively accepts and returns application/json (per the OpenAPI
            // contract). A controller-level [Consumes]/[Produces] attribute achieves the same
            // thing but also leaks "application/json" into every explicit ProducesResponseType
            // content type declared on the action (e.g. turning the 400/500 responses'
            // "application/problem+json" into two listed content types, and giving the
            // no-body 404 a phantom one) - registering it as a global filter instead avoids that.
            options.Filters.Add(new ConsumesAttribute("application/json"));
            options.Filters.Add(new ProducesAttribute("application/json"));
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        });

    // The System.Text.Json formatters otherwise also advertise text/json and the
    // application/*+json structured-syntax wildcard as acceptable, which both leaks into the
    // generated OpenAPI document's requestBody/response content types and lets callers negotiate
    // content types the contract doesn't actually support. PostConfigure runs after AddControllers
    // has populated the formatter list, regardless of registration order.
    builder.Services.PostConfigure<MvcOptions>(options =>
    {
        foreach (var supportedMediaTypes in options.InputFormatters.OfType<SystemTextJsonInputFormatter>()
            .Select(formatter => formatter.SupportedMediaTypes))
        {
            supportedMediaTypes.Clear();
            supportedMediaTypes.Add("application/json");
        }

        foreach (var supportedMediaTypes in options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>()
            .Select(formatter => formatter.SupportedMediaTypes))
        {
            supportedMediaTypes.Clear();
            supportedMediaTypes.Add("application/json");
        }
    });

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        // Every 400 response - whether raised explicitly by a controller or produced by
        // ASP.NET's own model binding (e.g. a malformed request body) - goes through the same
        // Problem Details shape required by the OpenAPI contract.
        options.InvalidModelStateResponseFactory = context => CodeRag.Api.Problems.ProblemResults.BadRequest(
            "The request body is missing or invalid.",
            context.HttpContext.Request.Path);

        // Without this, [ApiController] rewrites a bare NotFoundResult into a JSON Problem
        // Details body - the contract requires 404 responses to have no body at all.
        options.SuppressMapClientErrors = true;
    });

    builder.Services.AddOpenApi();

    // Kubernetes sets this on every pod automatically, so it doubles as a reliable "are we
    // running in a cluster" flag without needing extra config wiring.
    var isRunningInKubernetes = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") is not null;

    if (isRunningInKubernetes)
    {
        // Traefik terminates TLS and forwards to the pod as plain HTTP, so without this the
        // app thinks every request is http:// - which leaks into the OpenAPI document's server
        // URL and makes Scalar send its "try it" requests to http:// instead of https://.
        // The proxy's address is a cluster-internal pod IP that changes on every restart, so
        // there's no fixed address to pin as a known proxy - trust the whole path instead.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }

    builder.Services.AddApplication();
    builder.Services.AddDatabaseInfrastructure();

    builder.Services.AddEmbeddingAbstraction(builder.Configuration);
    builder.Services.AddLocalEmbeddingProvider();
    builder.Services.AddOllamaEmbeddingProvider();
    builder.Services.AddOpenAIEmbeddingProvider();

    // Exposes the same Projects/Code Query functionality as MCP tools, for LLM clients doing
    // code research, alongside the REST API. Stateless by default: no session affinity needed
    // since these tools never need to message the client back (no sampling/elicitation).
    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .AddCodeRagTools();

    var app = builder.Build();

    // Fail fast: resolving here forces provider validation (missing Provider/ApiKey/BaseUrl/
    // LocalModelPath, unreadable local model files, ...) to crash startup instead of surfacing
    // as a raw 500 on the first /code-queries request.
    app.Services.GetRequiredService<IEmbeddingGenerator>();

    if (isRunningInKubernetes)
    {
        app.UseForwardedHeaders();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    app.MapMcp("/mcp");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Entry point marker so <c>WebApplicationFactory&lt;Program&gt;</c> can bootstrap this API in tests.</summary>
public partial class Program
{
    // WebApplicationFactory<Program> only ever uses this type as a generic marker - which
    // requires a non-static class - and never actually instantiates it, so a protected
    // constructor satisfies Sonar's utility-class check without needing a public one.
    protected Program()
    {
    }
}
