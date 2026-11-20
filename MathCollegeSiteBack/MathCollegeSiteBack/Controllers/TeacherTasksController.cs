using MathCollegeSiteBack.Contracts;
using MathCollegeSiteBack.Models;
using MathCollegeSiteBack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathCollegeSiteBack.Controllers;

// Content-authoring endpoints are intended for the teacher on the local site.
[ApiController]
[Route("api/teacher/lessons/{lessonId:int}/tasks")]
public sealed class TeacherTasksController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeacherTaskDetails>>> GetTasks(
        int lessonId,
        CancellationToken cancellationToken)
    {
        var tasks = await db.LessonTasks.AsNoTracking()
            .Where(x => x.LessonId == lessonId)
            .OrderBy(x => x.Order)
            .Select(x => new TeacherTaskDetails(
                x.Id, x.LessonId, x.Order, x.Type, x.Prompt, x.ExplanationTitle, x.Explanation, x.Points,
                x.CorrectNumber, x.NumericTolerance,
                x.Options.OrderBy(option => option.Id)
                    .Select(option => new TeacherTaskOption(option.Id, option.Text, option.IsCorrect)).ToList()))
            .ToListAsync(cancellationToken);
        return Ok(tasks);
    }

    [HttpPost]
    public async Task<ActionResult<TeacherTaskDetails>> CreateTask(
        int lessonId,
        LessonTaskWriteRequest request,
        CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(lessonId, request, null, cancellationToken);
        if (error is not null) return BadRequest(new { message = error });
        if (await db.LessonRuns.AnyAsync(x => x.LessonId == lessonId, cancellationToken))
            return Conflict(new { message = "Урок уже проходили. Чтобы сохранить историю, добавьте новую версию урока." });

        var task = CreateEntity(lessonId, request);
        db.LessonTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetTasks), new { lessonId }, ToDetails(task));
    }

    [HttpPut("{taskId:int}")]
    public async Task<IActionResult> UpdateTask(
        int lessonId,
        int taskId,
        LessonTaskWriteRequest request,
        CancellationToken cancellationToken)
    {
        var task = await db.LessonTasks.Include(x => x.Options)
            .SingleOrDefaultAsync(x => x.Id == taskId && x.LessonId == lessonId, cancellationToken);
        if (task is null) return NotFound();

        var error = await ValidateAsync(lessonId, request, taskId, cancellationToken);
        if (error is not null) return BadRequest(new { message = error });
        if (await db.LessonRuns.AnyAsync(x => x.LessonId == lessonId, cancellationToken))
            return Conflict(new { message = "Нельзя менять задания после начала прохождений. Создайте новую версию урока." });

        task.Order = request.Order;
        task.Type = request.Type;
        task.Prompt = request.Prompt.Trim();
        task.ExplanationTitle = request.ExplanationTitle.Trim();
        task.Explanation = request.Explanation.Trim();
        task.Points = request.Points;
        task.CorrectNumber = request.CorrectNumber;
        task.NumericTolerance = request.NumericTolerance;
        db.TaskOptions.RemoveRange(task.Options);
        task.Options = (request.Options ?? []).Select(option => new TaskOption
        {
            Text = option.Text.Trim(),
            IsCorrect = option.IsCorrect
        }).ToList();

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:int}")]
    public async Task<IActionResult> DeleteTask(
        int lessonId,
        int taskId,
        CancellationToken cancellationToken)
    {
        var task = await db.LessonTasks.SingleOrDefaultAsync(
            x => x.Id == taskId && x.LessonId == lessonId,
            cancellationToken);
        if (task is null) return NotFound();
        if (await db.LessonRuns.AnyAsync(x => x.LessonId == lessonId, cancellationToken))
            return Conflict(new { message = "Нельзя удалять задания после начала прохождений. Создайте новую версию урока." });

        db.LessonTasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<string?> ValidateAsync(
        int lessonId,
        LessonTaskWriteRequest request,
        int? currentTaskId,
        CancellationToken cancellationToken)
    {
        if (!await db.Lessons.AnyAsync(x => x.Id == lessonId, cancellationToken))
            return "Урок не найден.";
        if (request.Order < 1)
            return "Номер задания должен быть положительным.";
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Trim().Length > 4000)
            return "Текст задания обязателен (до 4000 символов).";
        if (string.IsNullOrWhiteSpace(request.ExplanationTitle) || request.ExplanationTitle.Trim().Length > 200)
            return "Добавьте короткий заголовок объяснения.";
        if (string.IsNullOrWhiteSpace(request.Explanation) || request.Explanation.Trim().Length > 10000)
            return "Добавьте объяснение решения (до 10 000 символов).";
        if (request.Points is <= 0 or > 100)
            return "Баллы за задание должны быть больше 0 и не больше 100.";
        var isGenerated = TaskVariantGenerator.IsGeneratedType(request.Type);
        if (request.Type is not ("choice" or "number") && !isGenerated)
            return "Укажите choice, number или поддерживаемый тип generated: из каталога генераторов.";
        if (request.Type == "choice")
        {
            if (request.Options is null || request.Options.Count is < 2 or > 8)
                return "Для задания с выбором нужно от 2 до 8 вариантов ответа.";
            if (request.Options.Count(x => x.IsCorrect) != 1)
                return "Укажите ровно один правильный вариант.";
            if (request.Options.Any(x => string.IsNullOrWhiteSpace(x.Text) || x.Text.Trim().Length > 500))
                return "Текст каждого варианта ответа обязателен (до 500 символов).";
            if (request.CorrectNumber is not null || request.NumericTolerance != 0)
                return "Для задания с вариантами не заполняйте числовой ответ и допуск.";
        }
        else if (request.Type == "number")
        {
            if (request.CorrectNumber is null)
                return "Для числового задания укажите правильный ответ.";
            if (request.NumericTolerance < 0)
                return "Допуск не может быть отрицательным.";
            if (request.Options is { Count: > 0 })
                return "У числового задания не должно быть вариантов ответа.";
        }
        else
        {
            if (request.CorrectNumber is not null)
                return "У генерируемого задания правильный ответ рассчитывается автоматически.";
            if (request.NumericTolerance < 0)
                return "Допуск не может быть отрицательным.";
            if (request.Options is { Count: > 0 })
                return "Для генерируемого числового задания не добавляйте варианты ответа.";
        }

        var duplicateOrder = await db.LessonTasks.AnyAsync(
            x => x.LessonId == lessonId && x.Order == request.Order
                && (currentTaskId == null || x.Id != currentTaskId),
            cancellationToken);
        return duplicateOrder ? "В уроке уже есть задание с таким номером." : null;
    }

    private static LessonTask CreateEntity(int lessonId, LessonTaskWriteRequest request) => new()
    {
        LessonId = lessonId,
        Order = request.Order,
        Type = request.Type,
        Prompt = request.Prompt.Trim(),
        ExplanationTitle = request.ExplanationTitle.Trim(),
        Explanation = request.Explanation.Trim(),
        Points = request.Points,
        CorrectNumber = request.CorrectNumber,
        NumericTolerance = request.NumericTolerance,
        Options = (request.Options ?? []).Select(option => new TaskOption
        {
            Text = option.Text.Trim(),
            IsCorrect = option.IsCorrect
        }).ToList()
    };

    private static TeacherTaskDetails ToDetails(LessonTask task) => new(
        task.Id, task.LessonId, task.Order, task.Type, task.Prompt, task.ExplanationTitle, task.Explanation, task.Points,
        task.CorrectNumber, task.NumericTolerance,
        task.Options.Select(option => new TeacherTaskOption(option.Id, option.Text, option.IsCorrect)).ToList());
}
