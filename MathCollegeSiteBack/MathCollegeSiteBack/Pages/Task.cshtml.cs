using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace MathCollegeSiteBack.Pages;

public sealed class TaskModel : PageModel
{
    public int? LessonId { get; private set; }

    public IActionResult OnGet(int? lessonId)
    {
        LessonId = lessonId;
        return lessonId is null or <= 0 ? RedirectToPage("/Practice") : Page();
    }
}
