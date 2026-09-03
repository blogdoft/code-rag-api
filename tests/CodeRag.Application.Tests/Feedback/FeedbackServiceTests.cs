using CodeRag.Application.Feedback;
using CodeRag.Application.Projects;
using NSubstitute;
using Shouldly;

namespace CodeRag.Application.Tests.Feedback;

public sealed class FeedbackServiceTests
{
    private readonly IFeedbackRepository _feedbackRepository = Substitute.For<IFeedbackRepository>();
    private readonly IProjectsRepository _projectsRepository = Substitute.For<IProjectsRepository>();
    private readonly FeedbackService _sut;

    public FeedbackServiceTests()
    {
        _sut = new FeedbackService(_feedbackRepository, _projectsRepository);
        _projectsRepository.GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new Project(1, "some-project", null, null, DateTime.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationFailure_When_QuestionIsMissing(string? question)
    {
        var result = await _sut.SubmitAsync(1, question, true, [0.9], null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("400");
        await _feedbackRepository.DidNotReceive().InsertAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IReadOnlyList<double>>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_QuestionExceedsMaxLength()
    {
        var tooLong = new string('a', FeedbackService.MaxQuestionLength + 1);

        var result = await _sut.SubmitAsync(1, tooLong, true, [0.9], null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-question-too-long");
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_UsefulIsMissing()
    {
        var result = await _sut.SubmitAsync(1, "why is this slow?", null, [0.9], null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-useful-required");
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_SimilaritiesIsMissing()
    {
        var result = await _sut.SubmitAsync(1, "why is this slow?", true, null, null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-similarities-required");
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_TooManySimilaritiesAreProvided()
    {
        var tooMany = Enumerable.Repeat(0.5, FeedbackService.MaxSimilaritiesCount + 1).ToArray();

        var result = await _sut.SubmitAsync(1, "why is this slow?", true, tooMany, null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-too-many-similarities");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationFailure_When_UserIsMissing(string? user)
    {
        var result = await _sut.SubmitAsync(1, "why is this slow?", true, [0.9], null, user);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-user-required");
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_UserExceedsMaxLength()
    {
        var tooLong = new string('a', FeedbackService.MaxUserLength + 1);

        var result = await _sut.SubmitAsync(1, "why is this slow?", true, [0.9], null, tooLong);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-user-too-long");
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_ReasonExceedsMaxLength()
    {
        var tooLong = new string('a', FeedbackService.MaxReasonLength + 1);

        var result = await _sut.SubmitAsync(1, "why is this slow?", false, [0.2], tooLong, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-reason-too-long");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ProjectDoesNotExist()
    {
        _projectsRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.SubmitAsync(999, "why is this slow?", true, [0.9], null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("404");
    }

    [Fact]
    public async Task Should_SubmitFeedback_When_ReasonIsProvided()
    {
        var similarities = new[] { 0.9, 0.7 };
        var created = new FeedbackResult(1, 1, "why is this slow?", false, similarities, "not related", "claude code", DateTime.UtcNow);
        _feedbackRepository.InsertAsync(1, "why is this slow?", false, similarities, "not related", "claude code", Arg.Any<CancellationToken>())
            .Returns(created);

        var result = await _sut.SubmitAsync(1, "why is this slow?", false, similarities, "not related", "claude code");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(created);
    }

    [Fact]
    public async Task Should_SubmitFeedback_When_ReasonIsOmitted()
    {
        var similarities = new[] { 0.9 };
        var created = new FeedbackResult(1, 1, "why is this slow?", true, similarities, null, "codex", DateTime.UtcNow);
        _feedbackRepository.InsertAsync(1, "why is this slow?", true, similarities, null, "codex", Arg.Any<CancellationToken>())
            .Returns(created);

        var result = await _sut.SubmitAsync(1, "why is this slow?", true, similarities, null, "codex");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(created);
    }

    [Fact]
    public async Task Should_SubmitFeedback_When_SimilaritiesIsEmpty()
    {
        var created = new FeedbackResult(1, 1, "why is this slow?", false, [], "no results", "claude code", DateTime.UtcNow);
        _feedbackRepository.InsertAsync(1, "why is this slow?", false, Array.Empty<double>(), "no results", "claude code", Arg.Any<CancellationToken>())
            .Returns(created);

        var result = await _sut.SubmitAsync(1, "why is this slow?", false, Array.Empty<double>(), "no results", "claude code");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Similarities.ShouldBeEmpty();
    }
}
