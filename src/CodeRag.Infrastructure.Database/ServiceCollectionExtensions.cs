using CodeRag.Application.CodeQueries;
using CodeRag.Application.Feedback;
using CodeRag.Application.Projects;
using CodeRag.Infrastructure.Database.CodeQueries;
using CodeRag.Infrastructure.Database.Feedback;
using CodeRag.Infrastructure.Database.Projects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CodeRag.Infrastructure.Database;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Postgres/Dapper-backed repositories. This API only reads from the schema -
    /// it never runs migrations, so the database and its tables must already exist.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    public static IServiceCollection AddDatabaseInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            // Resolved lazily (on first use) rather than read from the IConfiguration passed in
            // at registration time, so that config sources added after this call - e.g. a test
            // host's in-memory overrides - are still picked up.
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("Database")
                ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Database' configuration value.");

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            return dataSourceBuilder.Build();
        });

        services.AddScoped<IProjectsRepository, ProjectsRepository>();
        services.AddScoped<ICodeDocumentsRepository, CodeDocumentsRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();

        return services;
    }
}
