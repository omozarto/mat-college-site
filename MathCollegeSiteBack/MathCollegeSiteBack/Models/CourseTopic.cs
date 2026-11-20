namespace MathCollegeSiteBack.Models;

public sealed class CourseTopic
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Slug { get; set; } = "";

    public string Description { get; set; } = "";

    public int Order { get; set; }

    public bool IsPublished { get; set; } = true;

    public List<LearningLesson> Lessons { get; set; } = [];
}