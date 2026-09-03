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

    [Fact]
    public async Task Should_UseLast30Days_When_NoDatesAreGiven()
    {
        _feedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var before = DateTime.UtcNow;
        var result = await _sut.GetStatsAsync(null, null, null);
        var after = DateTime.UtcNow;

        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBeInRange(before, after);
        (result.Value.EndDate - result.Value.StartDate).ShouldBe(TimeSpan.FromDays(FeedbackService.DefaultWindowDays));
    }

    [Fact]
    public async Task Should_DeriveEndDate_When_OnlyStartDateIsGiven()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _feedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.GetStatsAsync(start, null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBe(start.AddDays(FeedbackService.DefaultWindowDays));
    }

    [Fact]
    public async Task Should_DeriveStartDate_When_OnlyEndDateIsGiven()
    {
        var end = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        _feedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.GetStatsAsync(null, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBe(end);
        result.Value.StartDate.ShouldBe(end.AddDays(-FeedbackService.DefaultWindowDays));
    }

    [Fact]
    public async Task Should_UseExactWindow_When_BothDatesAreGiven()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        _feedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.GetStatsAsync(start, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBe(end);
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_StartDateIsAfterEndDate()
    {
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await _sut.GetStatsAsync(start, end, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-invalid-date-range");
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_WindowExceedsMaximum()
    {
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(FeedbackService.MaxWindowDays + 1);

        var result = await _sut.GetStatsAsync(start, end, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-window-too-large");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ProjectIdFilterDoesNotExist()
    {
        _projectsRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.GetStatsAsync(null, null, 999);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("404");
    }

    [Fact]
    public async Task Should_ReturnStats_When_ProjectIdFilterExists()
    {
        var weeks = new List<WeeklyFeedbackStats>
        {
            new(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 9), [new ProjectFeedbackStats(1, "some-project", 4, 3, 1, 75, 25)]),
        };
        _feedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), 1, Arg.Any<CancellationToken>())
            .Returns(weeks);

        var result = await _sut.GetStatsAsync(null, null, 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Weeks.ShouldBe(weeks);
    }

    [Fact]
    public async Task Should_UseStartOfCurrentMonthToNow_When_NoDatesAreGivenForExport()
    {
        _feedbackRepository.ExportAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var before = DateTime.UtcNow;
        var result = await _sut.ExportAsync(null, null, null);
        var after = DateTime.UtcNow;

        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBeInRange(before, after);
        result.Value.StartDate.ShouldBe(new DateTime(before.Year, before.Month, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Should_DefaultEndDateToNow_When_OnlyStartDateIsGivenForExport()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _feedbackRepository.ExportAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var before = DateTime.UtcNow;
        var result = await _sut.ExportAsync(start, null, null);
        var after = DateTime.UtcNow;

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBeInRange(before, after);
    }

    [Fact]
    public async Task Should_DefaultStartDateToStartOfCurrentMonth_When_OnlyEndDateIsGivenForExport()
    {
        var end = DateTime.UtcNow;
        _feedbackRepository.ExportAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.ExportAsync(null, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBe(end);
        result.Value.StartDate.ShouldBe(new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Should_UseExactWindow_When_BothDatesAreGivenForExport()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        _feedbackRepository.ExportAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.ExportAsync(start, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBe(end);
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_ExportEffectiveStartDateIsAfterEndDate()
    {
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await _sut.ExportAsync(start, end, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-invalid-date-range");
    }

    [Fact]
    public async Task Should_ReturnValidationFailure_When_ExportWindowExceedsMaximum()
    {
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(FeedbackService.MaxWindowDays + 1);

        var result = await _sut.ExportAsync(start, end, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-window-too-large");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ExportProjectIdFilterDoesNotExist()
    {
        _projectsRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.ExportAsync(null, null, 999);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldStartWith("404");
    }

    [Fact]
    public async Task Should_ReturnRows_When_ExportProjectIdFilterExists()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = new List<FeedbackExportRow>
        {
            new(1, 1, "some-project", "why is this slow?", true, [0.9], null, "claude code", start.AddDays(1)),
        };
        _feedbackRepository.ExportAsync(start, end, 1, Arg.Any<CancellationToken>()).Returns(rows);

        var result = await _sut.ExportAsync(start, end, 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Rows.ShouldBe(rows);
    }
}
