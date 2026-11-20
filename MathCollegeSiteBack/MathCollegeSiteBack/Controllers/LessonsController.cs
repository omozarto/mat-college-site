using MathCollegeSiteBack.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathCollegeSiteBack.Controllers;

[ApiController]
[Route("api/lessons")]
public sealed class LessonsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LessonListItem>>> GetLessons(CancellationToken cancellationToken)
    {
        var lessons = await db.Lessons.AsNoTracking()
            .Where(x => x.IsPublished && x.Topic.IsPublished)
            .OrderBy(x => x.TopicId).ThenBy(x => x.Order)
            .Select(x => new LessonListItem(x.Id, x.TopicId, x.Title, x.Summary, x.Tasks.Count))
            .ToListAsync(cancellationToken);

        return Ok(lessons);
    }

    [HttpGet("{lessonId:int}")]
    public async Task<ActionResult<LessonDetails>> GetLesson(int lessonId, CancellationToken cancellationToken)
    {
        var lesson = await db.Lessons.AsNoTracking()
            .Where(x => x.Id == lessonId && x.IsPublished && x.Topic.IsPublished)
            .Select(x => new LessonDetails(x.Id, x.TopicId, x.Title, x.Summary, x.Content, x.Tasks.Count))
            .SingleOrDefaultAsync(cancellationToken);

        return lesson is null ? NotFound() : Ok(lesson);
    }

    [HttpPost("{lessonId:int}/start")]
    public async Task<ActionResult<StartLessonResponse>> StartLesson(int lessonId, CancellationToken cancellationToken)
    {
        var lesson = await db.Lessons.AsNoTracking()
            .Where(x => x.Id == lessonId && x.IsPublished && x.Topic.IsPublished)
            .Select(x => new
            {
                x.Id,
                TaskCount = x.Tasks.Count,
                MaximumScore = x.Tasks.Sum(t => (decimal?)t.Points) ?? 0m
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (lesson is null || lesson.TaskCount == 0)
            return NotFound(new { message = "Занятие не найдено или в нём пока нет заданий." });

        var activeRun = await db.LessonRuns
            .Where(x => x.LessonId == lessonId && x.CompletedAtUtc == null && x.AbandonedAtUtc == null)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeRun is not null)
            return Ok(new StartLessonResponse(activeRun.Id, activeRun.LessonId, activeRun.CurrentTaskOrder));

        var run = new Models.LessonRun
        {
            LessonId = lessonId,
            StartedAtUtc = DateTime.UtcNow,
            CurrentTaskOrder = 1,
            MaximumScore = lesson.MaximumScore,
            RatingPercent = 100m
        };

        db.LessonRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new StartLessonResponse(run.Id, run.LessonId, run.CurrentTaskOrder));
    }

    [HttpPost]
    public async Task<ActionResult<LessonDetails>> CreateLesson(
        LessonWriteRequest request,
        CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(request, null, cancellationToken);
        if (error is not null) return BadRequest(new { message = error });

        var lesson = new Models.LearningLesson
        {
            TopicId = request.TopicId,
            Title = request.Title.Trim(),
            Summary = request.Summary.Trim(),
            Content = request.Content.Trim(),
            Order = request.Order,
            IsPublished = request.IsPublished
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetLesson), new { lessonId = lesson.Id },
            new LessonDetails(lesson.Id, lesson.TopicId, lesson.Title, lesson.Summary, lesson.Content, 0));
    }

    [HttpPut("{lessonId:int}")]
    public async Task<IActionResult> UpdateLesson(
        int lessonId,
        LessonWriteRequest request,
        CancellationToken cancellationToken)
    {
        var lesson = await db.Lessons.SingleOrDefaultAsync(x => x.Id == lessonId, cancellationToken);
        if (lesson is null) return NotFound();

        var error = await ValidateAsync(request, lessonId, cancellationToken);
        if (error is not null) return BadRequest(new { message = error });

        lesson.TopicId = request.TopicId;
        lesson.Title = request.Title.Trim();
        lesson.Summary = request.Summary.Trim();
        lesson.Content = request.Content.Trim();
        lesson.Order = request.Order;
        lesson.IsPublished = request.IsPublished;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{lessonId:int}")]
    public async Task<IActionResult> DeleteLesson(int lessonId, CancellationToken cancellationToken)
    {
        var lesson = await db.Lessons.SingleOrDefaultAsync(x => x.Id == lessonId, cancellationToken);
        if (lesson is null) return NotFound();
        if (await db.LessonRuns.AnyAsync(x => x.LessonId == lessonId, cancellationToken))
            return Conflict(new { message = "У урока уже есть история прохождения. Сначала скройте его вместо удаления." });

        db.Lessons.Remove(lesson);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<string?> ValidateAsync(
        LessonWriteRequest request,
        int? currentLessonId,
        CancellationToken cancellationToken)
    {
        if (request.TopicId < 1 || !await db.Topics.AnyAsync(x => x.Id == request.TopicId, cancellationToken))
            return "Выберите существующую тему.";
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200)
            return "Название урока обязательно и не должно быть длиннее 200 символов.";
        if (string.IsNullOrWhiteSpace(request.Summary) || request.Summary.Trim().Length > 1000)
            return "Добавьте краткое описание урока (до 1000 символов).";
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Trim().Length > 50000)
            return "Добавьте объяснение темы (до 50 000 символов).";
        if (request.Order < 1)
            return "Номер урока должен быть положительным.";

        var duplicateOrder = await db.Lessons.AnyAsync(
            x => x.TopicId == request.TopicId && x.Order == request.Order
                && (currentLessonId == null || x.Id != currentLessonId),
            cancellationToken);
        return duplicateOrder ? "В этой теме уже есть урок с таким номером." : null;
    }
}
