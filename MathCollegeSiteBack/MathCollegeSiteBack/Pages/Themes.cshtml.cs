using MathCollegeSiteBack.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MathCollegeSiteBack.Pages;

public sealed class ThemesModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<TopicItem> Topics { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var topics = await db.Topics
            .AsNoTracking()
            .Where(topic => topic.IsPublished)
            .OrderBy(topic => topic.Order)
            .Select(topic => new
            {
                topic.Id,
                topic.Name,
                topic.Description
            })
            .ToListAsync(cancellationToken);

        var lessons = await db.Lessons
            .AsNoTracking()
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

        var completedLessonIds = await db.LessonRuns
            .AsNoTracking()
            .Where(run => run.CompletedAtUtc != null)
            .Select(run => run.LessonId)
            .Distinct()
            .ToListAsync(cancellationToken);

        Topics = topics
            .Select(topic => new TopicItem(
                topic.Id,
                topic.Name,
                topic.Description,
                lessons
                    .Where(lesson => lesson.TopicId == topic.Id)
                    .Select(lesson => new LessonItem(
                        lesson.Id,
                        lesson.Title,
                        lesson.Summary,
                        lesson.TaskCount,
                        completedLessonIds.Contains(lesson.Id)))
                    .ToList()))
            .ToList();
    }

    public sealed record TopicItem(
        int Id,
        string Name,
        string Description,
        IReadOnlyList<LessonItem> Lessons);

    public sealed record LessonItem(
        int Id,
        string Title,
        string Summary,
        int TaskCount,
        bool IsCompleted);
}