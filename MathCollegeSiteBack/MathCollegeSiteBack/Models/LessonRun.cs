namespace MathCollegeSiteBack.Models;

public sealed class LessonRun
{
    public int Id { get; set; }

    public int LessonId { get; set; }

    public LearningLesson Lesson { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? AbandonedAtUtc { get; set; }

    public int CurrentTaskOrder { get; set; } = 1;

    public bool FinishWarningShown { get; set; }

    public decimal Score { get; set; }

    public decimal MaximumScore { get; set; }

    public int CorrectTasks { get; set; }

    public decimal? RatingPercent { get; set; }
}

public sealed class TaskAnswer
{
    public int Id { get; set; }

    public int LessonRunId { get; set; }

    public LessonRun LessonRun { get; set; } = null!;

    public int LessonTaskId { get; set; }

    public LessonTask LessonTask { get; set; } = null!;

    public int AttemptNumber { get; set; }

    public int? SelectedOptionId { get; set; }

    public decimal? SubmittedNumericAnswer { get; set; }

    public bool IsCorrect { get; set; }

    public bool IsSkipped { get; set; }

    public DateTime SubmittedAtUtc { get; set; }

    public DateTime? ExplanationViewedAtUtc { get; set; }
}

public sealed class TeacherStreak
{
    public int Id { get; set; }

    public int CurrentDays { get; set; }

    public DateOnly? LastLessonDate { get; set; }

    public DateOnly? PendingRecoveryDate { get; set; }
}

public sealed class StreakRecovery
{
    public int Id { get; set; }

    public DateOnly RecoveredDate { get; set; }

    public DateOnly UsedOn { get; set; }
}