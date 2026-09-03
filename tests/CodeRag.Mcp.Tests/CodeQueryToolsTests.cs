using Bogus;
using CodeRag.Application.CodeQueries;
using CodeRag.Application.Feedback;
using CodeRag.Mcp.Tools;
using ModelContextProtocol;
using NSubstitute;
using Shouldly;

namespace CodeRag.Mcp.Tests;

public sealed class CodeQueryToolsTests
{
    private readonly ICodeQueryService _codeQueryService = Substitute.For<ICodeQueryService>();
    private readonly IFeedbackService _feedbackService = Substitute.For<IFeedbackService>();
    private readonly Faker _faker = new();
    private readonly CodeQueryTools _sut;

    public CodeQueryToolsTests()
    {
        _sut = new CodeQueryTools(_codeQueryService, _feedbackService);
    }

    [Fact]
    public async Task Should_ReturnMappedResults_When_ServiceSucceeds()
    {
        const long projectId = 1;
        const string question = "where is the discount logic?";
        var match = new CodeQueryResult(
            1,
            _faker.System.FilePath(),
            "function",
            _faker.Hacker.Noun(),
            _faker.Hacker.Verb(),
            _faker.Lorem.Sentence(),
            0.9,
            _faker.Internet.Url(),
            _faker.Internet.Url());
        _codeQueryService.QueryAsync(
            projectId, question, null, null, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(new[] { match });

        var result = await _sut.QueryProjectCodeAsync(projectId, question, cancellationToken: CancellationToken.None);

        var item = result.ShouldHaveSingleItem();
        item.Id.ShouldBe(match.Id);
        item.SourceFile.ShouldBe(match.SourceFile);
        item.GitRawUrl.ShouldBe(match.GitRawUrl);
        item.GitUrl.ShouldBe(match.GitUrl);
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
        _codeQueryService.QueryAsync(
            projectId, "question", null, null, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(CodeQueryFailures.ProjectNotFound(projectId));

        var exception = await Should.ThrowAsync<McpException>(
            () => _sut.QueryProjectCodeAsync(projectId, "question", cancellationToken: CancellationToken.None));

        exception.Message.ShouldBe(CodeQueryFailures.ProjectNotFound(projectId).Message);
    }

    [Fact]
    public async Task Should_ThrowMcpException_When_QuestionIsBlank()
    {
        _codeQueryService.QueryAsync(
            1, "   ", null, null, null, null, null, null, null, null, Arg.Any<CancellationToken>())
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
        _codeQueryService.QueryAsync(
            projectId, question, limit, minSimilarity, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CodeQueryResult>());

        await _sut.QueryProjectCodeAsync(projectId, question, limit, minSimilarity, cancellationToken: CancellationToken.None);

        await _codeQueryService.Received(1).QueryAsync(
            projectId, question, limit, minSimilarity, null, null, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_PassKindNamespaceTypeNameFilters_When_Provided()
    {
        const long projectId = 1;
        const string question = "where is the discount logic?";
        _codeQueryService.QueryAsync(
            projectId, question, null, null,
            KindFilterOperator.Equals, "function",
            NamespaceFilterOperator.Contains, "Billing",
            TypeNameFilterOperator.NotContains, "Controller",
            Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CodeQueryResult>());

        await _sut.QueryProjectCodeAsync(
            projectId,
            question,
            kindOperator: KindFilterOperator.Equals,
            kindValue: "function",
            namespaceOperator: NamespaceFilterOperator.Contains,
            namespaceValue: "Billing",
            typeNameOperator: TypeNameFilterOperator.NotContains,
            typeNameValue: "Controller",
            cancellationToken: CancellationToken.None);

        await _codeQueryService.Received(1).QueryAsync(
            projectId, question, null, null,
            KindFilterOperator.Equals, "function",
            NamespaceFilterOperator.Contains, "Billing",
            TypeNameFilterOperator.NotContains, "Controller",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnMappedFeedback_When_ServiceSucceeds()
    {
        const long projectId = 1;
        const string question = "where is the discount logic?";
        var similarities = new[] { 0.9, 0.7 };
        var created = new FeedbackResult(1, projectId, question, true, similarities, null, "claude code", DateTime.UtcNow);
        _feedbackService.SubmitAsync(projectId, question, true, similarities, null, "claude code", Arg.Any<CancellationToken>())
            .Returns(created);

        var result = await _sut.SubmitCodeQueryFeedbackAsync(
            projectId, question, true, similarities, "claude code", cancellationToken: CancellationToken.None);

        result.Id.ShouldBe(created.Id);
        result.ProjectId.ShouldBe(created.ProjectId);
        result.Question.ShouldBe(created.Question);
        result.Useful.ShouldBe(created.Useful);
        result.Similarities.ShouldBe(created.Similarities);
        result.Reason.ShouldBe(created.Reason);
        result.User.ShouldBe(created.User);
    }

    [Fact]
    public async Task Should_ThrowMcpException_When_SubmittingFeedbackForProjectThatDoesNotExist()
    {
        const long projectId = 999;
        _feedbackService.SubmitAsync(projectId, "question", true, Arg.Any<IReadOnlyList<double>>(), null, "claude code", Arg.Any<CancellationToken>())
            .Returns(CodeQueryFailures.ProjectNotFound(projectId));

        var exception = await Should.ThrowAsync<McpException>(
            () => _sut.SubmitCodeQueryFeedbackAsync(
                projectId, "question", true, [0.9], "claude code", cancellationToken: CancellationToken.None));

        exception.Message.ShouldBe(CodeQueryFailures.ProjectNotFound(projectId).Message);
    }
}
