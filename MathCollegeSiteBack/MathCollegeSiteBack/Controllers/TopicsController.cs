using MathCollegeSiteBack.Contracts;
using MathCollegeSiteBack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathCollegeSiteBack.Controllers;

[ApiController]
[Route("api/topics")]
public sealed class TopicsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TopicListItem>>> GetTopics(CancellationToken cancellationToken)
    {
        var topics = await db.Topics.AsNoTracking()
            .Where(x => x.IsPublished)
            .OrderBy(x => x.Order)
            .Select(x => new TopicListItem(x.Id, x.Name, x.Slug, x.Description,
                x.Lessons.Count(lesson => lesson.IsPublished)))
            .ToListAsync(cancellationToken);
        return Ok(topics);
    }

    [HttpGet("{topicId:int}")]
    public async Task<ActionResult<TopicListItem>> GetTopic(int topicId, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.AsNoTracking()
            .Where(x => x.Id == topicId && x.IsPublished)
            .Select(x => new TopicListItem(x.Id, x.Name, x.Slug, x.Description,
                x.Lessons.Count(lesson => lesson.IsPublished)))
            .SingleOrDefaultAsync(cancellationToken);
        return topic is null ? NotFound() : Ok(topic);
    }

    [HttpGet("{topicId:int}/lessons")]
    public async Task<ActionResult<IReadOnlyList<LessonListItem>>> GetTopicLessons(
        int topicId,
        CancellationToken cancellationToken)
    {
        var topicExists = await db.Topics.AnyAsync(x => x.Id == topicId && x.IsPublished, cancellationToken);
        if (!topicExists) return NotFound();

        var lessons = await db.Lessons.AsNoTracking()
            .Where(x => x.TopicId == topicId && x.IsPublished)
            .OrderBy(x => x.Order)
            .Select(x => new LessonListItem(x.Id, x.TopicId, x.Title, x.Summary, x.Tasks.Count))
            .ToListAsync(cancellationToken);
        return Ok(lessons);
    }

    [HttpPost]
    public async Task<ActionResult<TopicListItem>> CreateTopic(
        TopicWriteRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null) return BadRequest(new { message = validationError });

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Topics.AnyAsync(x => x.Slug == slug || x.Order == request.Order, cancellationToken))
            return Conflict(new { message = "Тема с таким адресом или номером уже существует." });

        var topic = new CourseTopic
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description.Trim(),
            Order = request.Order,
            IsPublished = request.IsPublished
        };
        db.Topics.Add(topic);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetTopic), new { topicId = topic.Id },
            new TopicListItem(topic.Id, topic.Name, topic.Slug, topic.Description, 0));
    }

    [HttpPut("{topicId:int}")]
    public async Task<IActionResult> UpdateTopic(
        int topicId,
        TopicWriteRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null) return BadRequest(new { message = validationError });

        var topic = await db.Topics.SingleOrDefaultAsync(x => x.Id == topicId, cancellationToken);
        if (topic is null) return NotFound();

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Topics.AnyAsync(x => x.Id != topicId && (x.Slug == slug || x.Order == request.Order), cancellationToken))
            return Conflict(new { message = "Другая тема уже использует этот адрес или номер." });

        topic.Name = request.Name.Trim();
        topic.Slug = slug;
        topic.Description = request.Description.Trim();
        topic.Order = request.Order;
        topic.IsPublished = request.IsPublished;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{topicId:int}")]
    public async Task<IActionResult> DeleteTopic(int topicId, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.SingleOrDefaultAsync(x => x.Id == topicId, cancellationToken);
        if (topic is null) return NotFound();
        if (await db.Lessons.AnyAsync(x => x.TopicId == topicId, cancellationToken))
            return Conflict(new { message = "Сначала перенесите или удалите уроки из этой темы." });

        db.Topics.Remove(topic);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? Validate(TopicWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            return "Название темы обязательно и должно быть не длиннее 200 символов.";
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 1000)
            return "Добавьте краткое описание темы (до 1000 символов).";
        if (request.Order < 1)
            return "Номер темы должен быть положительным.";
        if (string.IsNullOrWhiteSpace(request.Slug))
            return "Адрес темы обязателен.";
        var slug = request.Slug.Trim();
        if (slug.Length is < 2 or > 100 || slug.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
            return "Адрес темы укажите латинскими буквами, цифрами и дефисами.";
        return null;
    }
}
