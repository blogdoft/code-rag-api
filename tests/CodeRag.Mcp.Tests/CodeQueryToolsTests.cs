using Bogus;
using CodeRag.Application.CodeQueries;
using CodeRag.Mcp.Tools;
using ModelContextProtocol;
using NSubstitute;
using Shouldly;

namespace CodeRag.Mcp.Tests;

public sealed class CodeQueryToolsTests
{
    private readonly ICodeQueryService _codeQueryService = Substitute.For<ICodeQueryService>();
    private readonly Faker _faker = new();
    private readonly CodeQueryTools _sut;

    public CodeQueryToolsTests()
    {
        _sut = new CodeQueryTools(_codeQueryService);
    }

    [Fact]
    public async Task Should_ReturnMappedResults_When_ServiceSucceeds()
    {
        const long projectId = 1;
        const string question = "where is the discount logic?";
        var match = new CodeQueryResult(
            1, _faker.System.FilePath(), "function", _faker.Hacker.Noun(), _faker.Hacker.Verb(), _faker.Lorem.Sentence(), 0.9);
        _codeQueryService.QueryAsync(projectId, question, null, null, Arg.Any<CancellationToken>()).Returns(new[] { match });

        var result = await _sut.QueryProjectCodeAsync(projectId, question, cancellationToken: CancellationToken.None);

        var item = result.ShouldHaveSingleItem();
        item.Id.ShouldBe(match.Id);
        item.SourceFile.ShouldBe(match.SourceFile);
        item.Kind.ShouldBe(match.Kind);
        item.TypeName.ShouldBe(match.TypeName);
        item.Member.ShouldBe(match.Member);
        item.EmbeddingText.ShouldBe(match.EmbeddingText);
        item.Similarity.ShouldBe(match.Similarity);
    }

    [Fact]
    public async Task Should_ThrowMcpException_When_ProjectDoesNotExist()
    {
        const long projectId = 999;
        _codeQueryService.QueryAsync(projectId, "question", null, null, Arg.Any<CancellationToken>())
            .Returns(CodeQueryFailures.ProjectNotFound(projectId));

        var exception = await Should.ThrowAsync<McpException>(
            () => _sut.QueryProjectCodeAsync(projectId, "question", cancellationToken: CancellationToken.None));

        exception.Message.ShouldBe(CodeQueryFailures.ProjectNotFound(projectId).Message);
    }

    [Fact]
    public async Task Should_ThrowMcpException_When_QuestionIsBlank()
    {
        _codeQueryService.QueryAsync(1, "   ", null, null, Arg.Any<CancellationToken>())
            .Returns(CodeQueryFailures.QuestionRequired);

        await Should.ThrowAsync<McpException>(
            () => _sut.QueryProjectCodeAsync(1, "   ", cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task Should_PassLimitAndMinSimilarity_When_Provided()
    {
        const long projectId = 1;
        const string question = "where is the discount logic?";
        const int limit = 5;
        const double minSimilarity = 0.5;
        _codeQueryService.QueryAsync(projectId, question, limit, minSimilarity, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CodeQueryResult>());

        await _sut.QueryProjectCodeAsync(projectId, question, limit, minSimilarity, CancellationToken.None);

        await _codeQueryService.Received(1).QueryAsync(projectId, question, limit, minSimilarity, Arg.Any<CancellationToken>());
    }
}
