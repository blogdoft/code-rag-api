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

        await _projectsRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_QuestionExceedsMaxLength()
    {
        var tooLong = new string('a', CodeQueryService.MaxQuestionLength + 1);

        var result = await _sut.QueryAsync(1, tooLong);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Fact]
    public async Task Should_NotCheckProjectExistence_When_QuestionExceedsMaxLength()
    {
        var tooLong = new string('a', CodeQueryService.MaxQuestionLength + 1);

        await _sut.QueryAsync(1, tooLong);

        await _projectsRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AcceptQuestionAtMaxLength_When_LengthIsExactlyTheLimit()
    {
        const long projectId = 1;
        var atLimit = new string('a', CodeQueryService.MaxQuestionLength);
        var embedding = new EmbeddingVector([0.1f, 0.2f, 0.3f]);

        _projectsRepository.GetByIdAsync(projectId, Arg.Any<CancellationToken>()).Returns(CreateProject(projectId));
        _embeddingGenerator.GenerateAsync(atLimit, Arg.Any<CancellationToken>()).Returns(embedding);
        _codeDocumentsRepository.SearchAsync(
            projectId, "Ollama", "bge-m3", 3, embedding.values, CodeQueryService.ResultLimit, null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.QueryAsync(projectId, atLimit);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReturnNotFoundFailure_When_ProjectDoesNotExist()
    {
        _projectsRepository.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.QueryAsync(42, "where is the discount logic?");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("404");
    }

    [Fact]
    public async Task Should_NotGenerateEmbedding_When_ProjectDoesNotExist()
    {
        _projectsRepository.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns((Project?)null);

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

        _projectsRepository.GetByIdAsync(projectId, Arg.Any<CancellationToken>()).Returns(CreateProject(projectId));
        _embeddingGenerator.GenerateAsync(question, Arg.Any<CancellationToken>()).Returns(embedding);
        _codeDocumentsRepository.SearchAsync(
            projectId, "Ollama", "bge-m3", 3, embedding.values, CodeQueryService.ResultLimit, null, Arg.Any<CancellationToken>())
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
        _projectsRepository.GetByIdAsync(projectId, Arg.Any<CancellationToken>()).Returns(CreateProject(projectId));
        _embeddingGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(embedding);
        _codeDocumentsRepository.SearchAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<float>>(), Arg.Any<int>(), Arg.Any<double?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.QueryAsync(projectId, "some question");

        await _codeDocumentsRepository.Received(1).SearchAsync(
            projectId, "Ollama", "bge-m3", 3, embedding.values, CodeQueryService.ResultLimit, null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CodeQueryService.MaxResultLimit + 1)]
    public async Task Should_ReturnValidationFailure_When_LimitIsOutOfRange(int limit)
    {
        var result = await _sut.QueryAsync(1, "some question", limit: limit);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task Should_ReturnValidationFailure_When_MinSimilarityIsOutOfRange(double minSimilarity)
    {
        var result = await _sut.QueryAsync(1, "some question", minSimilarity: minSimilarity);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
    }

    [Fact]
    public async Task Should_PassExplicitLimitAndMinSimilarity_When_Provided()
    {
        const long projectId = 1;
        const int limit = 25;
        const double minSimilarity = 0.6;
        var embedding = new EmbeddingVector([0.1f, 0.2f, 0.3f]);
        _projectsRepository.GetByIdAsync(projectId, Arg.Any<CancellationToken>()).Returns(CreateProject(projectId));
        _embeddingGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(embedding);
        _codeDocumentsRepository.SearchAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<float>>(), Arg.Any<int>(), Arg.Any<double?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.QueryAsync(projectId, "some question", limit, minSimilarity);

        await _codeDocumentsRepository.Received(1).SearchAsync(
            projectId, "Ollama", "bge-m3", 3, embedding.values, limit, minSimilarity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_MapProjectGitUrlAndConcatenateGitRawUrlWithSourceFile_When_ProjectHasGitUrls()
    {
        const long projectId = 1;
        const string gitUrl = "https://github.com/example-org/shopping-cart-service.git";
        const string gitRawUrl = "https://raw.githubusercontent.com/example-org/shopping-cart-service/main";
        const string sourceFile = "src/cart/pricing/discount_calculator.py";
        var embedding = new EmbeddingVector([0.1f, 0.2f, 0.3f]);

        _projectsRepository.GetByIdAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new Project(projectId, "shopping-cart-service", gitUrl, gitRawUrl, DateTime.UtcNow));
        _embeddingGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(embedding);
        _codeDocumentsRepository.SearchAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<float>>(), Arg.Any<int>(), Arg.Any<double?>(), Arg.Any<CancellationToken>())
            .Returns([new CodeQueryResult(1, sourceFile, "function", "DiscountCalculator", "apply", "some text", 0.9)]);

        var result = await _sut.QueryAsync(projectId, "some question");

        var mapped = result.Value.Single();
        mapped.GitUrl.ShouldBe(gitUrl);
        mapped.GitRawUrl.ShouldBe($"{gitRawUrl}/{sourceFile}");
    }

    [Fact]
    public async Task Should_ReturnNullGitRawUrl_When_ProjectGitRawUrlIsNull()
    {
        const long projectId = 1;
        var embedding = new EmbeddingVector([0.1f, 0.2f, 0.3f]);

        _projectsRepository.GetByIdAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new Project(projectId, "shopping-cart-service", null, null, DateTime.UtcNow));
        _embeddingGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(embedding);
        _codeDocumentsRepository.SearchAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<float>>(), Arg.Any<int>(), Arg.Any<double?>(), Arg.Any<CancellationToken>())
            .Returns([new CodeQueryResult(1, "src/cart/pricing/discount_calculator.py", "function", "DiscountCalculator", "apply", "some text", 0.9)]);

        var result = await _sut.QueryAsync(projectId, "some question");

        var mapped = result.Value.Single();
        mapped.GitUrl.ShouldBeNull();
        mapped.GitRawUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Should_ReturnNullGitRawUrl_When_SourceFileIsNull()
    {
        const long projectId = 1;
        const string gitRawUrl = "https://raw.githubusercontent.com/example-org/shopping-cart-service/main";
        var embedding = new EmbeddingVector([0.1f, 0.2f, 0.3f]);

        _projectsRepository.GetByIdAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new Project(projectId, "shopping-cart-service", null, gitRawUrl, DateTime.UtcNow));
        _embeddingGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(embedding);
        _codeDocumentsRepository.SearchAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<float>>(), Arg.Any<int>(), Arg.Any<double?>(), Arg.Any<CancellationToken>())
            .Returns([new CodeQueryResult(1, null, "function", "DiscountCalculator", "apply", "some text", 0.9)]);

        var result = await _sut.QueryAsync(projectId, "some question");

        result.Value.Single().GitRawUrl.ShouldBeNull();
    }

    private Project CreateProject(long id) => new(id, _faker.Company.CompanyName(), null, null, DateTime.UtcNow);

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
