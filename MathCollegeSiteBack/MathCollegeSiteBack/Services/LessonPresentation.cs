using System.Text.RegularExpressions;
using MathCollegeSiteBack.Contracts;

namespace MathCollegeSiteBack.Services;

public static partial class LessonPresentation
{
    private static readonly (string Kind, string Title, string[] Labels)[] Sections =
    [
        ("idea", "Главная идея", ["Основная идея", "Идея"]),
        ("formula", "Правило и формула", ["Формула и алгоритм", "Формула", "Правило"]),
        ("example", "Разберём пример", ["Разобранный пример", "Пример"]),
        ("visual", "Посмотрим на схему", ["Наглядное объяснение", "Наглядно"]),
        ("caution", "Где легко ошибиться", ["Типичная ошибка", "Важно"]),
        ("summary", "Короткий итог", ["Итог урока", "Итог"])
    ];

    public static IReadOnlyList<LessonContentBlock> BuildBlocks(string? content)
    {
        var paragraphs = (content ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var blocks = new List<LessonContentBlock>();
        var fallbackIndex = 0;
        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph)) continue;
            var sectionIndex = Array.FindIndex(Sections, section => section.Labels.Any(label =>
                paragraph.StartsWith(label + ".", StringComparison.OrdinalIgnoreCase)
                || paragraph.StartsWith(label + ":", StringComparison.OrdinalIgnoreCase)));
            var section = sectionIndex >= 0 ? Sections[sectionIndex] : Sections[Math.Min(fallbackIndex, Sections.Length - 1)];
            fallbackIndex++;
            var text = paragraph;
            foreach (var label in section.Labels)
            {
                if (text.StartsWith(label, StringComparison.OrdinalIgnoreCase))
                {
                    text = text[label.Length..].TrimStart('.', ':', ' ');
                    break;
                }
            }
            if (text.Length > 0) blocks.Add(new LessonContentBlock(section.Kind, section.Title, text));
        }
        return blocks;
    }

    public static string VisualKind(string? text)
    {
        var value = (text ?? "").ToLowerInvariant();
        if (value.Contains("дерев") || value.Contains("перестанов") || value.Contains("сочетан") || value.Contains("вариант")) return "tree";
        if (value.Contains("таблиц") || value.Contains("условн") || value.Contains("байес")) return "table";
        if (value.Contains("частот") || value.Contains("распредел") || value.Contains("средн") || value.Contains("график")) return "chart";
        if (value.Contains("вероят") || value.Contains("исход") || value.Contains("событ") || value.Contains("дол") || value.Contains("процент")) return "probability";
        return "steps";
    }

    public static IReadOnlyList<string> BuildSolutionSteps(string? explanation)
    {
        return (explanation ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(line => SentenceRegex().Split(line))
            .Select(step => step.Trim())
            .Where(step => step.Length > 0)
            .ToArray();
    }

    [GeneratedRegex("(?<=[.!?])\\s+")]
    private static partial Regex SentenceRegex();
}
