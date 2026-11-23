namespace MathCollegeSiteBack.Contracts;

public sealed record TopicListItem(int Id, string Name, string Slug, string Description, int LessonCount);
public sealed record TopicWriteRequest(string Name, string Slug, string Description, int Order, bool IsPublished = true);
public sealed record LessonWriteRequest(int TopicId, string Title, string Summary, string Content, int Order, bool IsPublished = true);
public sealed record TaskOptionWriteRequest(string Text, bool IsCorrect);
public sealed record LessonTaskWriteRequest(
    int Order,
    string Type,
    string Prompt,
    string ExplanationTitle,
    string Explanation,
    decimal Points,
    decimal? CorrectNumber,
    decimal NumericTolerance,
    IReadOnlyList<TaskOptionWriteRequest>? Options);
public sealed record TeacherTaskOption(int Id, string Text, bool IsCorrect);
public sealed record TeacherTaskDetails(
    int Id,
    int LessonId,
    int Order,
    string Type,
    string Prompt,
    string ExplanationTitle,
    string Explanation,
    decimal Points,
    decimal? CorrectNumber,
    decimal NumericTolerance,
    IReadOnlyList<TeacherTaskOption> Options);
public sealed record LessonListItem(int Id, int TopicId, string Title, string Summary, int TaskCount);
public sealed record LessonContentBlock(string Kind, string Title, string Text);
public sealed record LessonDetails(int Id, int TopicId, string Title, string Summary, string Content, int TaskCount,
    IReadOnlyList<LessonContentBlock>? ContentBlocks = null, string VisualKind = "steps");
public sealed record LessonRunSummary(
    int RunId,
    int LessonId,
    string LessonTitle,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? AbandonedAtUtc,
    decimal Score,
    decimal MaximumScore,
    decimal? RatingPercent,
    int CorrectTasks,
    int TotalTasks);
public sealed record StartLessonResponse(int RunId, int LessonId, int CurrentTaskOrder);
public sealed record AnswerRequest(int? OptionId, decimal? NumericAnswer);
public sealed record TaskOptionResponse(int Id, string Text);
public sealed record CurrentTaskResponse(
    int RunId,
    int LessonId,
    int TaskId,
    int TaskOrder,
    int TaskCount,
    string Type,
    string Prompt,
    IReadOnlyList<TaskOptionResponse> Options,
    int Attempts,
    bool ExplanationRequired,
    string? ExplanationTitle,
    string? Explanation,
    IReadOnlyList<string>? SolutionSteps = null,
    string VisualKind = "steps");
public sealed record AnswerResponse(
    bool IsCorrect,
    bool LessonRestarted,
    bool ExplanationRequired,
    bool RetryAllowed,
    string Message,
    string? ExplanationTitle,
    string? Explanation,
    string? LessonContent,
    int? NewRunId,
    decimal? PointsAwarded,
    decimal? Score,
    decimal? MaximumScore,
    decimal? RatingPercent,
    int? CorrectTasks,
    int? TotalTasks,
    int? AttemptsUsed,
    int? AttemptsRemaining);
public sealed record FinishLessonResponse(
    bool LessonCompleted,
    bool ReviewRequired,
    bool ConfirmationRequired,
    int? TaskId,
    int? TaskOrder,
    string Message,
    string? ExplanationTitle,
    string? Explanation,
    int? AttemptsUsed,
    int? AttemptsRemaining,
    decimal? Score,
    decimal? MaximumScore,
    decimal? RatingPercent,
    int? RetakesUsed,
    int? CurrentStreak,
    bool? NeedsStreakRecovery,
    int? RecoveriesLeftThisMonth);
public sealed record ExitLessonRequest(bool Confirm);
public sealed record StreakStatusResponse(
    int Streak,
    bool IsLit,
    bool NeedsLessonToRestore,
    bool RecoveryPending,
    int RecoveriesLeftThisMonth);
