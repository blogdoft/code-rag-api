using CodeRag.Application.CodeQueries;
using CodeRag.Application.Feedback;
using CodeRag.Application.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CodeRag.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProjectsService, ProjectsService>();
        services.AddScoped<ICodeQueryService, CodeQueryService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        return services;
    }
}
