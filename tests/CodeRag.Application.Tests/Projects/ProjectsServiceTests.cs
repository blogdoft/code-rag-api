using Bogus;
using CodeRag.Application.CodeQueries;
using CodeRag.Application.Projects;
using NSubstitute;
using Shouldly;

namespace CodeRag.Application.Tests.Projects;

public sealed class ProjectsServiceTests
{
    private readonly IProjectsRepository _repository = Substitute.For<IProjectsRepository>();
    private readonly ICodeDocumentsRepository _codeDocumentsRepository = Substitute.For<ICodeDocumentsRepository>();
    private readonly Faker _faker = new();
    private readonly ProjectsService _sut;

    public ProjectsServiceTests()
    {
        _sut = new ProjectsService(_repository, _codeDocumentsRepository);
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

    [Fact]
    public async Task Should_ReturnProject_When_ProjectExists()
    {
        var project = CreateProjects(1).Single();
        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var result = await _sut.GetAsync(project.Id);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(project);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_GettingProjectThatDoesNotExist()
    {
        _repository.GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.GetAsync(999);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("404");
    }

    [Fact]
    public async Task Should_CreateProject_When_NameIsValidAndUnique()
    {
        var name = _faker.Commerce.ProductName();
        var created = new Project(1, name, null, null, DateTime.UtcNow);
        _repository.NameExistsAsync(name, null, Arg.Any<CancellationToken>()).Returns(false);
        _repository.InsertAsync(name, null, null, Arg.Any<CancellationToken>()).Returns(created);

        var result = await _sut.CreateAsync(name);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(created);
    }

    [Fact]
    public async Task Should_CreateProject_When_GitUrlAndGitRawUrlAreProvided()
    {
        var name = _faker.Commerce.ProductName();
        var gitUrl = _faker.Internet.Url();
        var gitRawUrl = _faker.Internet.Url();
        var created = new Project(1, name, gitUrl, gitRawUrl, DateTime.UtcNow);
        _repository.NameExistsAsync(name, null, Arg.Any<CancellationToken>()).Returns(false);
        _repository.InsertAsync(name, gitUrl, gitRawUrl, Arg.Any<CancellationToken>()).Returns(created);

        var result = await _sut.CreateAsync(name, gitUrl, gitRawUrl);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(created);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationFailure_When_CreatingProjectWithoutName(string? name)
    {
        var result = await _sut.CreateAsync(name);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
        await _repository.DidNotReceive().InsertAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_CreatingProjectWithNameExceedingMaxLength()
    {
        var tooLong = new string('a', ProjectsService.MaxNameLength + 1);

        var result = await _sut.CreateAsync(tooLong);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Fact]
    public async Task Should_ReturnConflict_When_CreatingProjectWithDuplicateName()
    {
        var name = _faker.Commerce.ProductName();
        _repository.NameExistsAsync(name, null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.CreateAsync(name);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("409");
        await _repository.DidNotReceive().InsertAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_CreatingProjectWithBlankGitUrl()
    {
        var result = await _sut.CreateAsync(_faker.Commerce.ProductName(), gitUrl: "   ");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_CreatingProjectWithGitUrlExceedingMaxLength()
    {
        var tooLong = new string('a', ProjectsService.MaxGitUrlLength + 1);

        var result = await _sut.CreateAsync(_faker.Commerce.ProductName(), gitUrl: tooLong);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_CreatingProjectWithBlankGitRawUrl()
    {
        var result = await _sut.CreateAsync(_faker.Commerce.ProductName(), gitRawUrl: "   ");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Fact]
    public async Task Should_UpdateProject_When_NameIsValidAndUnique()
    {
        var name = _faker.Commerce.ProductName();
        var updated = new Project(1, name, null, null, DateTime.UtcNow);
        _repository.NameExistsAsync(name, 1, Arg.Any<CancellationToken>()).Returns(false);
        _repository.UpdateAsync(1, name, null, null, Arg.Any<CancellationToken>()).Returns(updated);

        var result = await _sut.UpdateAsync(1, name);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(updated);
    }

    [Fact]
    public async Task Should_UpdateProject_When_GitUrlAndGitRawUrlAreProvided()
    {
        var name = _faker.Commerce.ProductName();
        var gitUrl = _faker.Internet.Url();
        var gitRawUrl = _faker.Internet.Url();
        var updated = new Project(1, name, gitUrl, gitRawUrl, DateTime.UtcNow);
        _repository.NameExistsAsync(name, 1, Arg.Any<CancellationToken>()).Returns(false);
        _repository.UpdateAsync(1, name, gitUrl, gitRawUrl, Arg.Any<CancellationToken>()).Returns(updated);

        var result = await _sut.UpdateAsync(1, name, gitUrl, gitRawUrl);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(updated);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_UpdatingProjectThatDoesNotExist()
    {
        var name = _faker.Commerce.ProductName();
        _repository.NameExistsAsync(name, 999, Arg.Any<CancellationToken>()).Returns(false);
        _repository.UpdateAsync(999, name, null, null, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.UpdateAsync(999, name);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("404");
    }

    [Fact]
    public async Task Should_ReturnConflict_When_UpdatingProjectWithDuplicateName()
    {
        var name = _faker.Commerce.ProductName();
        _repository.NameExistsAsync(name, 1, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.UpdateAsync(1, name);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("409");
        await _repository.DidNotReceive().UpdateAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_UpdatingProjectWithoutName()
    {
        var result = await _sut.UpdateAsync(1, "   ");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Fact]
    public async Task Should_DeleteProject_When_ProjectHasNoIndexedCodeDocuments()
    {
        _codeDocumentsRepository.ExistsForProjectAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        _repository.DeleteAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.DeleteAsync(1);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_DeletingProjectThatDoesNotExist()
    {
        _codeDocumentsRepository.ExistsForProjectAsync(999, Arg.Any<CancellationToken>()).Returns(false);
        _repository.DeleteAsync(999, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.DeleteAsync(999);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("404");
    }

    [Fact]
    public async Task Should_ReturnConflict_When_DeletingProjectWithIndexedCodeDocuments()
    {
        _codeDocumentsRepository.ExistsForProjectAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.DeleteAsync(1);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("409");
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    private Project[] CreateProjects(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new Project(i, _faker.Commerce.ProductName(), null, null, _faker.Date.PastOffset().UtcDateTime))
            .ToArray();
}
