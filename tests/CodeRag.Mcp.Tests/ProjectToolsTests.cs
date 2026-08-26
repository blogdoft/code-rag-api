using Bogus;
using CodeRag.Application.Projects;
using CodeRag.Mcp.Tools;
using ModelContextProtocol;
using NSubstitute;
using Shouldly;

namespace CodeRag.Mcp.Tests;

public sealed class ProjectToolsTests
{
    private readonly IProjectsService _projectsService = Substitute.For<IProjectsService>();
    private readonly Faker _faker = new();
    private readonly ProjectTools _sut;

    public ProjectToolsTests()
    {
        _sut = new ProjectTools(_projectsService);
    }

    [Fact]
    public async Task Should_ReturnMappedProjects_When_ServiceSucceeds()
    {
        var project = new Project(1, _faker.Commerce.ProductName(), _faker.Date.PastOffset().UtcDateTime);
        _projectsService.ListAsync(null, Arg.Any<CancellationToken>()).Returns(new[] { project });

        var result = await _sut.ListProjects(null, CancellationToken.None);

        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe(project.Id);
        result[0].Name.ShouldBe(project.Name);
        result[0].CreatedAt.ShouldBe(project.CreatedAt);
    }

    [Fact]
    public async Task Should_PassNameFilterThrough_When_Provided()
    {
        const string filter = "cart";
        _projectsService.ListAsync(filter, Arg.Any<CancellationToken>()).Returns(Array.Empty<Project>());

        await _sut.ListProjects(filter, CancellationToken.None);

        await _projectsService.Received(1).ListAsync(filter, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowMcpException_When_ServiceReturnsFailure()
    {
        _projectsService.ListAsync("", Arg.Any<CancellationToken>()).Returns(ProjectFailures.NameFilterEmpty);

        var exception = await Should.ThrowAsync<McpException>(() => _sut.ListProjects("", CancellationToken.None));

        exception.Message.ShouldBe(ProjectFailures.NameFilterEmpty.Message);
    }
}
