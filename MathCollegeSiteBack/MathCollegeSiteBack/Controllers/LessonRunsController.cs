using MathCollegeSiteBack.Contracts;
using MathCollegeSiteBack.Models;
using MathCollegeSiteBack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathCollegeSiteBack.Controllers;

[ApiController]
[Route("api/runs")]
public sealed class LessonRunsController(AppDbContext db) : ControllerBase
{
    private const int MaximumAttemptsPerTask = 3; // Первое решение + две пересдачи.

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LessonRunSummary>>> GetRecentRuns(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var runs = await db.LessonRuns.AsNoTracking()
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(limit)
            .Select(x => new LessonRunSummary(
                x.Id, x.LessonId, x.Lesson.Title, x.StartedAtUtc, x.CompletedAtUtc, x.AbandonedAtUtc,
                x.Score, x.MaximumScore, x.RatingPercent, x.CorrectTasks, x.Lesson.Tasks.Count))
            .ToListAsync(cancellationToken);
        return Ok(runs);
    }

    [HttpGet("{runId:int}")]
    public async Task<ActionResult<LessonRunSummary>> GetRun(int runId, CancellationToken cancellationToken)
    {
        var run = await db.LessonRuns.AsNoTracking()
            .Where(x => x.Id == runId)
            .Select(x => new LessonRunSummary(
                x.Id, x.LessonId, x.Lesson.Title, x.StartedAtUtc, x.CompletedAtUtc, x.AbandonedAtUtc,
                x.Score, x.MaximumScore, x.RatingPercent, x.CorrectTasks, x.Lesson.Tasks.Count))
            .SingleOrDefaultAsync(cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("{runId:int}/current")]
    public async Task<ActionResult<CurrentTaskResponse>> GetCurrentTask(int runId, CancellationToken cancellationToken)
    {
        var run = await db.LessonRuns.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null) return NotFound();
        if (run.CompletedAtUtc is not null || run.AbandonedAtUtc is not null)
            return Conflict(new { message = "Этот проход занятия уже завершён." });

        var taskCount = await TaskCountAsync(run.LessonId, cancellationToken);
        var current = await db.LessonTasks.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.LessonId == run.LessonId && x.Order == run.CurrentTaskOrder,
                cancellationToken);
        if (current is null)
            return Conflict(new { message = "Все задания просмотрены. Нажмите «Завершить занятие»." });

        var variant = TaskVariantGenerator.Create(run.Id, current);

        var options = await db.TaskOptions.AsNoTracking()
            .Where(x => x.LessonTaskId == current.Id)
            .OrderBy(x => x.Id)
            .Select(x => new TaskOptionResponse(x.Id, x.Text))
            .ToListAsync(cancellationToken);

        var lastInteraction = await db.TaskAnswers.AsNoTracking()
            .Where(x => x.LessonRunId == run.Id && x.LessonTaskId == current.Id)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var attemptCount = await db.TaskAnswers.CountAsync(
            x => x.LessonRunId == run.Id && x.LessonTaskId == current.Id && !x.IsSkipped,
            cancellationToken);

        var explanationAvailable = lastInteraction is { IsSkipped: true } or { IsCorrect: false };
        return Ok(new CurrentTaskResponse(
            run.Id, run.LessonId, current.Id, current.Order, taskCount, variant.Type, variant.Prompt, options,
            attemptCount,
            explanationAvailable && lastInteraction!.ExplanationViewedAtUtc is null,
            explanationAvailable ? variant.ExplanationTitle : null,
            explanationAvailable ? variant.Explanation : null,
            explanationAvailable ? LessonPresentation.BuildSolutionSteps(variant.Explanation) : [],
            LessonPresentation.VisualKind(variant.Prompt + " " + variant.Explanation)));
    }

    [HttpPost("{runId:int}/tasks/{taskId:int}/answer")]
    public async Task<ActionResult<AnswerResponse>> SubmitAnswer(
        int runId,
        int taskId,
        AnswerRequest request,
        CancellationToken cancellationToken)
    {
        var run = await GetActiveRunAsync(runId, cancellationToken);
        if (run is null) return NotFound(new { message = "Активное занятие не найдено." });

        var task = await db.LessonTasks.SingleOrDefaultAsync(
            x => x.Id == taskId && x.LessonId == run.LessonId && x.Order == run.CurrentTaskOrder,
            cancellationToken);
        if (task is null) return Conflict(new { message = "Это задание сейчас нельзя отправить." });

        var variant = TaskVariantGenerator.Create(run.Id, task);
        TaskOption? option = null;
        bool isCorrect;
        if (task.Type == "choice")
        {
            if (request.OptionId is null || request.NumericAnswer is not null)
                return BadRequest(new { message = "Выберите один из вариантов ответа." });
            option = await db.TaskOptions.SingleOrDefaultAsync(
                x => x.Id == request.OptionId && x.LessonTaskId == task.Id,
                cancellationToken);
            if (option is null) return BadRequest(new { message = "Такого варианта ответа нет." });
            isCorrect = option.IsCorrect;
        }
        else if (task.Type == "number" || TaskVariantGenerator.IsGeneratedType(task.Type))
        {
            if (request.NumericAnswer is null || request.OptionId is not null)
                return BadRequest(new { message = "Введите числовой ответ." });
            isCorrect = variant.CorrectNumber.HasValue
                && Math.Abs(request.NumericAnswer.Value - variant.CorrectNumber.Value) <= task.NumericTolerance;
        }
        else
        {
            return Conflict(new { message = "Этот тип задания пока не поддерживается." });
        }

        var lastInteraction = await db.TaskAnswers
            .Where(x => x.LessonRunId == run.Id && x.LessonTaskId == task.Id)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastInteraction is { IsSkipped: true, ExplanationViewedAtUtc: null }
            or { IsCorrect: false, ExplanationViewedAtUtc: null })
            return Conflict(new { message = "Перед новой попыткой прочитайте объяснение решения." });

        var attemptsUsed = await db.TaskAnswers.CountAsync(
            x => x.LessonRunId == run.Id && x.LessonTaskId == task.Id && !x.IsSkipped,
            cancellationToken);
        if (attemptsUsed >= MaximumAttemptsPerTask)
            return Conflict(new { message = "Три попытки на это задание уже использованы." });

        var attemptNumber = attemptsUsed + 1;
        var alreadySolved = await db.TaskAnswers.AnyAsync(
            x => x.LessonRunId == run.Id && x.LessonTaskId == task.Id && x.IsCorrect,
            cancellationToken);

        if (attemptNumber > 1)
            run.RatingPercent = Math.Max(0m, (run.RatingPercent ?? 100m) - 10m);

        var answer = new TaskAnswer
        {
            LessonRunId = run.Id,
            LessonTaskId = task.Id,
            AttemptNumber = attemptNumber,
            SelectedOptionId = option?.Id,
            IsCorrect = isCorrect,
            SubmittedNumericAnswer = request.NumericAnswer,
            SubmittedAtUtc = DateTime.UtcNow
        };
        db.TaskAnswers.Add(answer);

        if (!isCorrect && attemptNumber == MaximumAttemptsPerTask)
        {
            var lesson = await db.Lessons.SingleAsync(x => x.Id == run.LessonId, cancellationToken);
            run.AbandonedAtUtc = DateTime.UtcNow;
            var newRun = new LessonRun
            {
                LessonId = run.LessonId,
                StartedAtUtc = DateTime.UtcNow,
                CurrentTaskOrder = 1,
                MaximumScore = run.MaximumScore,
                RatingPercent = 100m
            };
            db.LessonRuns.Add(newRun);
            await db.SaveChangesAsync(cancellationToken);

            return Ok(new AnswerResponse(
                false, true, true, false,
                "Три попытки использованы. Кажется, тема пока не усвоилась. Начнём урок заново с объяснения.",
                variant.ExplanationTitle, variant.Explanation, lesson.Content, newRun.Id,
                null, 0m, newRun.MaximumScore, 100m, 0, await TaskCountAsync(run.LessonId, cancellationToken),
                MaximumAttemptsPerTask, 0));
        }

        if (!isCorrect)
        {
            await db.SaveChangesAsync(cancellationToken);
            return Ok(new AnswerResponse(
                false, false, true, true,
                "Ответ неверный. Прочитайте объяснение и попробуйте ещё раз.",
                variant.ExplanationTitle, variant.Explanation, null, null, null,
                run.Score, run.MaximumScore, run.RatingPercent, run.CorrectTasks,
                await TaskCountAsync(run.LessonId, cancellationToken), attemptNumber,
                MaximumAttemptsPerTask - attemptNumber));
        }

        if (!alreadySolved)
        {
            run.Score += task.Points;
            run.CorrectTasks++;
        }
        run.CurrentTaskOrder = Math.Max(run.CurrentTaskOrder, task.Order + 1);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new AnswerResponse(
            true, false, false, false,
            "Верно! Переходим к следующему заданию.", null, null, null, null,
            task.Points, run.Score, run.MaximumScore, run.RatingPercent, run.CorrectTasks,
            await TaskCountAsync(run.LessonId, cancellationToken), attemptNumber,
            MaximumAttemptsPerTask - attemptNumber));
    }

    [HttpPost("{runId:int}/tasks/{taskId:int}/skip")]
    public async Task<IActionResult> SkipTask(int runId, int taskId, CancellationToken cancellationToken)
    {
        var run = await GetActiveRunAsync(runId, cancellationToken);
        if (run is null) return NotFound(new { message = "Активное занятие не найдено." });

        var task = await db.LessonTasks.SingleOrDefaultAsync(
            x => x.Id == taskId && x.LessonId == run.LessonId && x.Order == run.CurrentTaskOrder,
            cancellationToken);
        if (task is null) return Conflict(new { message = "Это задание сейчас нельзя пропустить." });

        var lastInteraction = await db.TaskAnswers
            .Where(x => x.LessonRunId == run.Id && x.LessonTaskId == task.Id)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastInteraction is { IsSkipped: true, ExplanationViewedAtUtc: null }
            or { IsCorrect: false, ExplanationViewedAtUtc: null })
            return Conflict(new { message = "Сначала прочитайте объяснение к заданию." });

        var skipRecorded = await db.TaskAnswers.AnyAsync(
            x => x.LessonRunId == run.Id && x.LessonTaskId == task.Id && x.IsSkipped,
            cancellationToken);
        if (!skipRecorded)
        {
            db.TaskAnswers.Add(new TaskAnswer
            {
                LessonRunId = run.Id,
                LessonTaskId = task.Id,
                AttemptNumber = 0,
                IsSkipped = true,
                IsCorrect = false,
                SubmittedAtUtc = DateTime.UtcNow
            });
        }

        run.CurrentTaskOrder++;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { currentTaskOrder = run.CurrentTaskOrder });
    }

    [HttpPost("{runId:int}/tasks/{taskId:int}/explanation-viewed")]
    public async Task<IActionResult> MarkExplanationViewed(int runId, int taskId, CancellationToken cancellationToken)
    {
        var run = await GetActiveRunAsync(runId, cancellationToken);
        if (run is null) return NotFound();

        var currentTaskId = await db.LessonTasks
            .Where(x => x.LessonId == run.LessonId && x.Order == run.CurrentTaskOrder)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (currentTaskId != taskId)
            return Conflict(new { message = "Это не текущее задание." });

        var lastInteraction = await db.TaskAnswers
            .Where(x => x.LessonRunId == run.Id && x.LessonTaskId == taskId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastInteraction is null || (lastInteraction.IsCorrect && !lastInteraction.IsSkipped))
            return Conflict(new { message = "Для этого задания сейчас нет ожидающего объяснения." });

        lastInteraction.ExplanationViewedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{runId:int}/finish")]
    public async Task<ActionResult<FinishLessonResponse>> FinishLesson(int runId, CancellationToken cancellationToken)
    {
        var run = await GetActiveRunAsync(runId, cancellationToken);
        if (run is null) return NotFound(new { message = "Активное занятие не найдено." });

        var tasks = await db.LessonTasks.Where(x => x.LessonId == run.LessonId)
            .OrderBy(x => x.Order).ToListAsync(cancellationToken);
        var solvedTaskIds = await db.TaskAnswers
            .Where(x => x.LessonRunId == run.Id && x.IsCorrect)
            .Select(x => x.LessonTaskId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var unresolved = tasks.FirstOrDefault(x => !solvedTaskIds.Contains(x.Id));

        if (unresolved is not null)
        {
            run.CurrentTaskOrder = unresolved.Order;
            if (run.FinishWarningShown)
            {
                await db.SaveChangesAsync(cancellationToken);
                var attemptsAtLimit = await AttemptsUsedAsync(run.Id, unresolved.Id, cancellationToken);
                return Ok(new FinishLessonResponse(
                    false, false, true, unresolved.Id, unresolved.Order,
                    "В занятии остались нерешённые задания. Прогресс будет потерян. Точно хочешь выйти?",
                    null, null, attemptsAtLimit, Math.Max(0, MaximumAttemptsPerTask - attemptsAtLimit), null, null,
                    run.RatingPercent, null, null, null, null));
            }

            run.FinishWarningShown = true;
            var lastInteraction = await db.TaskAnswers
                .Where(x => x.LessonRunId == run.Id && x.LessonTaskId == unresolved.Id)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (lastInteraction is null)
            {
                lastInteraction = new TaskAnswer
                {
                    LessonRunId = run.Id,
                    LessonTaskId = unresolved.Id,
                    AttemptNumber = 0,
                    IsSkipped = true,
                    IsCorrect = false,
                    SubmittedAtUtc = DateTime.UtcNow
                };
                db.TaskAnswers.Add(lastInteraction);
            }
            lastInteraction.ExplanationViewedAtUtc = null;
            await db.SaveChangesAsync(cancellationToken);

            var used = await AttemptsUsedAsync(run.Id, unresolved.Id, cancellationToken);
            return Ok(new FinishLessonResponse(
                false, true, false, unresolved.Id, unresolved.Order,
                "Не все задания решены. Разберём это задание ещё раз, затем попробуй ответить повторно.",
                TaskVariantGenerator.Create(run.Id, unresolved).ExplanationTitle,
                TaskVariantGenerator.Create(run.Id, unresolved).Explanation,
                used, Math.Max(0, MaximumAttemptsPerTask - used), null, null,
                run.RatingPercent, null, null, null, null));
        }

        run.CompletedAtUtc = DateTime.UtcNow;
        run.RatingPercent ??= 100m;
        var retakesUsed = (int)((100m - run.RatingPercent.Value) / 10m);
        var streakResult = await ApplyCompletedLessonToStreakAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new FinishLessonResponse(
            true, false, false, null, null,
            streakResult.NeedsRecovery
                ? "Все задания решены. Серия прервалась — можно восстановить огонёк."
                : "Урок завершён! Все задания решены.",
            null, null, null, null, run.Score, run.MaximumScore, run.RatingPercent,
            retakesUsed, streakResult.Streak, streakResult.NeedsRecovery, streakResult.RecoveriesLeft));
    }

    [HttpPost("{runId:int}/exit")]
    public async Task<IActionResult> ExitLesson(int runId, ExitLessonRequest request, CancellationToken cancellationToken)
    {
        var run = await GetActiveRunAsync(runId, cancellationToken);
        if (run is null) return NotFound(new { message = "Активное занятие не найдено." });

        if (!request.Confirm)
            return Ok(new { exited = false, runId = run.Id, currentTaskOrder = run.CurrentTaskOrder });

        db.LessonRuns.Remove(run); // TaskAnswers удалятся каскадно, как и обещает диалог.
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { exited = true, progressLost = true, message = "Проход урока завершён, прогресс удалён." });
    }

    private Task<LessonRun?> GetActiveRunAsync(int runId, CancellationToken cancellationToken) =>
        db.LessonRuns.SingleOrDefaultAsync(
            x => x.Id == runId && x.CompletedAtUtc == null && x.AbandonedAtUtc == null,
            cancellationToken);

    private Task<int> TaskCountAsync(int lessonId, CancellationToken cancellationToken) =>
        db.LessonTasks.CountAsync(x => x.LessonId == lessonId, cancellationToken);

    private Task<int> AttemptsUsedAsync(int runId, int taskId, CancellationToken cancellationToken) =>
        db.TaskAnswers.CountAsync(
            x => x.LessonRunId == runId && x.LessonTaskId == taskId && !x.IsSkipped,
            cancellationToken);

    private async Task<(int Streak, bool NeedsRecovery, int RecoveriesLeft)> ApplyCompletedLessonToStreakAsync(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var streak = await db.TeacherStreaks.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        var recoveriesLeft = await RecoveriesLeftAsync(today, cancellationToken);

        if (streak is null)
        {
            db.TeacherStreaks.Add(new TeacherStreak { Id = 1, CurrentDays = 1, LastLessonDate = today });
            return (1, false, recoveriesLeft);
        }

        if (streak.LastLessonDate == today)
            return (streak.CurrentDays, false, recoveriesLeft);

        var daysSinceLastLesson = streak.LastLessonDate is null
            ? int.MaxValue
            : today.DayNumber - streak.LastLessonDate.Value.DayNumber;

        if (daysSinceLastLesson == 1)
        {
            streak.CurrentDays++;
            streak.LastLessonDate = today;
            streak.PendingRecoveryDate = null;
            return (streak.CurrentDays, false, recoveriesLeft);
        }

        if (daysSinceLastLesson == 2 && recoveriesLeft > 0)
        {
            streak.PendingRecoveryDate = today;
            return (0, true, recoveriesLeft);
        }

        streak.CurrentDays = 1;
        streak.LastLessonDate = today;
        streak.PendingRecoveryDate = null;
        return (1, false, recoveriesLeft);
    }

    private async Task<int> RecoveriesLeftAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var firstDay = new DateOnly(today.Year, today.Month, 1);
        var nextMonth = firstDay.AddMonths(1);
        var used = await db.StreakRecoveries.CountAsync(
            x => x.UsedOn >= firstDay && x.UsedOn < nextMonth,
            cancellationToken);
        return Math.Max(0, 5 - used);
    }
}
