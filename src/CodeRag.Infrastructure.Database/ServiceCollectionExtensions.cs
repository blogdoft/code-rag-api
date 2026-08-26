using CodeRag.Application.CodeQueries;
using CodeRag.Application.Projects;
using CodeRag.Infrastructure.Database.CodeQueries;
using CodeRag.Infrastructure.Database.Projects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CodeRag.Infrastructure.Database;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Postgres/Dapper-backed repositories. This API only reads from the schema -
    /// it never runs migrations, so the database and its tables must already exist.
    /// </summary>
    public static IServiceCollection AddDatabaseInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(options.ConnectionString);
            dataSourceBuilder.UseVector();
            return dataSourceBuilder.Build();
        });

        services.AddScoped<IProjectsRepository, ProjectsRepository>();
        services.AddScoped<ICodeDocumentsRepository, CodeDocumentsRepository>();

        return services;
    }
}
