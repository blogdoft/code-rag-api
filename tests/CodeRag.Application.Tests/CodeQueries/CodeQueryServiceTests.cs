using Bogus;
using CodeRag.Application.CodeQueries;
using CodeRag.Application.Projects;
using CodeRag.Embeddings.Abstraction;
using NSubstitute;
using Shouldly;

namespace CodeRag.Application.Tests.CodeQueries;

public sealed class CodeQueryServiceTests
{
    private readonly IProjectsRepository _projectsRepository = Substitute.For<IProjectsRepository>();
    private readonly ICodeDocumentsRepository _codeDocumentsRepository = Substitute.For<ICodeDocumentsRepository>();
    private readonly IEmbeddingGenerator _embeddingGenerator = Substitute.For<IEmbeddingGenerator>();
    private readonly Faker _faker = new();
    private readonly CodeQueryService _sut;

    public CodeQueryServiceTests()
    {
        _sut = new CodeQueryService(_projectsRepository, _codeDocumentsRepository, _embeddingGenerator);

        _embeddingGenerator.Provider.Returns("Ollama");
        _embeddingGenerator.Model.Returns("bge-m3");
        _embeddingGenerator.Dimensions.Returns(3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationFailure_When_QuestionIsNullOrBlank(string? question)
    {
        var result = await _sut.QueryAsync(1, question);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Fact]
    public async Task Should_NotCheckProjectExistence_When_QuestionIsBlank()
    {
        await _sut.QueryAsync(1, string.Empty);

        await _projectsRepository.DidNotReceive().ExistsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnNotFoundFailure_When_ProjectDoesNotExist()
    {
        _projectsRepository.ExistsAsync(42, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.QueryAsync(42, "where is the discount logic?");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("404");
    }

    [Fact]
    public async Task Should_NotGenerateEmbedding_When_ProjectDoesNotExist()
    {
        _projectsRepository.ExistsAsync(42, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.QueryAsync(42, "where is the discount logic?");

        await _embeddingGenerator.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnMatchingDocuments_When_ProjectExistsAndQuestionIsValid()
    {
        const long projectId = 1;
        const string question = "where is the discount logic?";
        var embedding = new EmbeddingVector([0.1f, 0.2f, 0.3f]);
        var expected = CreateResults(2);

        _projectsRepository.ExistsAsync(projectId, Arg.Any<CancellationToken>()).Returns(true);
        _embeddingGenerator.GenerateAsync(question, Arg.Any<CancellationToken>()).Returns(embedding);
        _codeDocumentsRepository.SearchAsync(
            projectId, "Ollama", "bge-m3", 3, embedding.values, CodeQueryService.ResultLimit, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _sut.QueryAsync(projectId, question);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task Should_SearchWithConfiguredEmbeddingModel_When_GeneratingEmbeddingSucceeds()
    {
        const long projectId = 7;
        var embedding = new EmbeddingVector([1f, 2f, 3f]);
        _projectsRepository.ExistsAsync(projectId, Arg.Any<CancellationToken>()).Returns(true);
        _embeddingGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(embedding);
        _codeDocumentsRepository.SearchAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<float>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.QueryAsync(projectId, "some question");

        await _codeDocumentsRepository.Received(1).SearchAsync(
            projectId, "Ollama", "bge-m3", 3, embedding.values, CodeQueryService.ResultLimit, Arg.Any<CancellationToken>());
    }

    private CodeQueryResult[] CreateResults(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new CodeQueryResult(
                i,
                _faker.System.FilePath(),
                "function",
                _faker.Hacker.Noun(),
                _faker.Hacker.Verb(),
                _faker.Lorem.Sentence(),
                _faker.Random.Double(-1, 1)))
            .ToArray();
}
