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
        _codeQueryService.QueryAsync(projectId, question, Arg.Any<CancellationToken>()).Returns(new[] { match });

        var result = await _sut.QueryProjectCode(projectId, question, CancellationToken.None);

        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe(match.Id);
        result[0].SourceFile.ShouldBe(match.SourceFile);
        result[0].Kind.ShouldBe(match.Kind);
        result[0].TypeName.ShouldBe(match.TypeName);
        result[0].Member.ShouldBe(match.Member);
        result[0].EmbeddingText.ShouldBe(match.EmbeddingText);
        result[0].Similarity.ShouldBe(match.Similarity);
    }

    [Fact]
    public async Task Should_ThrowMcpException_When_ProjectDoesNotExist()
    {
        const long projectId = 999;
        _codeQueryService.QueryAsync(projectId, "question", Arg.Any<CancellationToken>())
            .Returns(CodeQueryFailures.ProjectNotFound(projectId));

        var exception = await Should.ThrowAsync<McpException>(
            () => _sut.QueryProjectCode(projectId, "question", CancellationToken.None));

        exception.Message.ShouldBe(CodeQueryFailures.ProjectNotFound(projectId).Message);
    }

    [Fact]
    public async Task Should_ThrowMcpException_When_QuestionIsBlank()
    {
        _codeQueryService.QueryAsync(1, "   ", Arg.Any<CancellationToken>())
            .Returns(CodeQueryFailures.QuestionRequired);

        await Should.ThrowAsync<McpException>(() => _sut.QueryProjectCode(1, "   ", CancellationToken.None));
    }
}
