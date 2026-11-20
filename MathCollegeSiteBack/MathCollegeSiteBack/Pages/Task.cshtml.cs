using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MathCollegeSiteBack.Pages;

public sealed class TaskModel : PageModel
{
    public int? LessonId { get; private set; }

    public void OnGet(int? lessonId)
    {
        LessonId = lessonId;
    }
}