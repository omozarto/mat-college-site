namespace MathCollegeSiteBack.Models;

public sealed class LearningLesson
{
    public int Id { get; set; }

    public int TopicId { get; set; }

    public CourseTopic Topic { get; set; } = null!;

    public string Title { get; set; } = "";

    public string Summary { get; set; } = "";

    public string Content { get; set; } = "";

    public int Order { get; set; }

    public bool IsPublished { get; set; } = true;

    public List<LessonTask> Tasks { get; set; } = [];
}

public sealed class LessonTask
{
    public int Id { get; set; }

    public int LessonId { get; set; }

    public LearningLesson Lesson { get; set; } = null!;

    public int Order { get; set; }

    public string Type { get; set; } = "choice";

    public string Prompt { get; set; } = "";

    public string ExplanationTitle { get; set; } = "Разберём решение";

    public string Explanation { get; set; } = "";

    public decimal Points { get; set; } = 1m;

    public decimal? CorrectNumber { get; set; }

    public decimal NumericTolerance { get; set; }

    public List<TaskOption> Options { get; set; } = [];
}

public sealed class TaskOption
{
    public int Id { get; set; }

    public int LessonTaskId { get; set; }

    public LessonTask LessonTask { get; set; } = null!;

    public string Text { get; set; } = "";

    public bool IsCorrect { get; set; }
}