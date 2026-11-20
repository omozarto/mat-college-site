using MathCollegeSiteBack.Contracts;
using MathCollegeSiteBack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathCollegeSiteBack.Controllers;

[ApiController]
[Route("api/streak")]
public sealed class StreakController(AppDbContext db) : ControllerBase
{
    // The old placeholder page posted here directly. Keep it non-mutating so
    // a click cannot grant a streak without completing every lesson task.
    [HttpPost("complete")]
    public IActionResult RejectManualCompletion() => Conflict(new
    {
        success = false,
        message = "Огонёк засчитывается только после завершения всех заданий занятия."
    });

    [HttpGet("status")]
    public async Task<ActionResult<StreakStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var streak = await db.TeacherStreaks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        var recoveriesLeft = await RecoveriesLeftAsync(today, cancellationToken);

        if (streak is null || streak.LastLessonDate is null)
            return Ok(new StreakStatusResponse(0, false, false, false, recoveriesLeft));

        var daysSinceLastLesson = today.DayNumber - streak.LastLessonDate.Value.DayNumber;
        var isLit = daysSinceLastLesson is 0 or 1;
        var canRecoverOneMissedDay = daysSinceLastLesson == 2 && recoveriesLeft > 0;
        var recoveryPending = streak.PendingRecoveryDate == today;

        return Ok(new StreakStatusResponse(
            isLit ? streak.CurrentDays : 0,
            isLit,
            canRecoverOneMissedDay && !recoveryPending,
            recoveryPending,
            recoveriesLeft));
    }

    [HttpPost("recover")]
    public async Task<IActionResult> Recover(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var streak = await db.TeacherStreaks.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (streak is null || streak.PendingRecoveryDate != today)
            return Conflict(new { message = "Сначала завершите занятие. После этого можно будет восстановить серию." });

        var recoveriesLeft = await RecoveriesLeftAsync(today, cancellationToken);
        if (recoveriesLeft == 0)
            return Conflict(new { message = "В этом месяце все восстановления уже использованы." });

        var missedDate = today.AddDays(-1);
        if (streak.LastLessonDate != missedDate.AddDays(-1))
            return Conflict(new { message = "Восстановить можно серию после одного пропущенного дня." });

        streak.CurrentDays += 2; // Считаем восстановленный день и сегодняшнее завершённое занятие.
        streak.LastLessonDate = today;
        streak.PendingRecoveryDate = null;
        db.StreakRecoveries.Add(new StreakRecovery { RecoveredDate = missedDate, UsedOn = today });
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            streak = streak.CurrentDays,
            recoveriesLeft = recoveriesLeft - 1,
            message = $"Серия восстановлена! В этом месяце осталось {recoveriesLeft - 1} восстановлений."
        });
    }

    [HttpPost("start-over")]
    public async Task<IActionResult> StartOver(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var streak = await db.TeacherStreaks.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (streak is null || streak.PendingRecoveryDate != today)
            return Conflict(new { message = "Нет ожидающего решения о восстановлении серии." });

        streak.CurrentDays = 1;
        streak.LastLessonDate = today;
        streak.PendingRecoveryDate = null;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, streak = 1, message = "Начинаем новую серию с одного дня." });
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
