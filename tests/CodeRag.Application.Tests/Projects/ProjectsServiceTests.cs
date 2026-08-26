using Bogus;
using CodeRag.Application.Projects;
using NSubstitute;
using Shouldly;

namespace CodeRag.Application.Tests.Projects;

public sealed class ProjectsServiceTests
{
    private readonly IProjectsRepository _repository = Substitute.For<IProjectsRepository>();
    private readonly Faker _faker = new();
    private readonly ProjectsService _sut;

    public ProjectsServiceTests()
    {
        _sut = new ProjectsService(_repository);
    }

    [Fact]
    public async Task Should_ReturnAllProjects_When_NameFilterIsNull()
    {
        var projects = CreateProjects(3);
        _repository.SearchAsync(null, Arg.Any<CancellationToken>()).Returns(projects);

        var result = await _sut.ListAsync(null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(projects);
    }

    [Fact]
    public async Task Should_ReturnFilteredProjects_When_NameFilterIsProvided()
    {
        const string filter = "cart";
        var projects = CreateProjects(2);
        _repository.SearchAsync(filter, Arg.Any<CancellationToken>()).Returns(projects);

        var result = await _sut.ListAsync(filter);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(projects);
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_NameFilterIsEmpty()
    {
        var result = await _sut.ListAsync(string.Empty);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_NameFilterExceedsMaxLength()
    {
        var tooLong = new string('a', ProjectsService.MaxNameFilterLength + 1);

        var result = await _sut.ListAsync(tooLong);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Fact]
    public async Task Should_NotQueryRepository_When_NameFilterIsInvalid()
    {
        await _sut.ListAsync(string.Empty);

        await _repository.DidNotReceive().SearchAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AcceptNameFilterAtMaxLength_When_LengthIsExactlyTheLimit()
    {
        var atLimit = new string('a', ProjectsService.MaxNameFilterLength);
        _repository.SearchAsync(atLimit, Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.ListAsync(atLimit);

        result.IsSuccess.ShouldBeTrue();
    }

    private Project[] CreateProjects(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new Project(i, _faker.Commerce.ProductName(), _faker.Date.PastOffset().UtcDateTime))
            .ToArray();
}
