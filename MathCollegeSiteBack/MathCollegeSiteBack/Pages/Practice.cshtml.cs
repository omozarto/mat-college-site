using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MathCollegeSiteBack.Pages;

public sealed class PracticeModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<PracticeTopic> Topics { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var topics = await db.Topics.AsNoTracking()
            .Where(topic => topic.IsPublished)
            .OrderBy(topic => topic.Order)
            .Select(topic => new { topic.Id, topic.Name, topic.Description })
            .ToListAsync(cancellationToken);

        var lessons = await db.Lessons.AsNoTracking()
            .Where(lesson => lesson.IsPublished && lesson.Topic.IsPublished)
            .OrderBy(lesson => lesson.Order)
            .Select(lesson => new
            {
                lesson.Id,
                lesson.TopicId,
                lesson.Title,
                lesson.Summary,
                TaskCount = lesson.Tasks.Count
            })
            .ToListAsync(cancellationToken);

        Topics = topics.Select(topic => new PracticeTopic(
            topic.Id,
            topic.Name,
            topic.Description,
            lessons.Where(lesson => lesson.TopicId == topic.Id)
                .Select(lesson => new PracticeLesson(
                    lesson.Id, lesson.Title, lesson.Summary, lesson.TaskCount))
                .ToList()))
            .ToList();
    }

    public sealed record PracticeTopic(
        int Id,
        string Name,
        string Description,
        IReadOnlyList<PracticeLesson> Lessons);

    public sealed record PracticeLesson(int Id, string Title, string Summary, int TaskCount);
}
