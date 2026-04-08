using System.Globalization;
using TrainerStudApp.Domain;

namespace TrainerStudApp.Services;

public sealed record TaskGradeResult(int TaskId, string Expected, string Actual, bool IsCorrect);

public static class ShortAnswerAutoGrader
{
    public static IReadOnlyList<TaskGradeResult> Grade(
        BlankTemplateDefinition template,
        IReadOnlyDictionary<int, string> answersByTaskNumber)
    {
        var list = new List<TaskGradeResult>();
        foreach (var entry in template.AutoAnswers)
        {
            answersByTaskNumber.TryGetValue(entry.TaskId, out var actual);
            actual ??= string.Empty;
            var ok = NormalizeMatch(entry.Answer, actual);
            list.Add(new TaskGradeResult(entry.TaskId, entry.Answer, actual, ok));
        }

        return list;
    }

    public static bool NormalizeMatch(string expected, string actual)
    {
        var e = Normalize(expected);
        var a = Normalize(actual);
        return string.Equals(e, a, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string s)
    {
        var t = s.Trim();
        t = t.Replace('\u00A0', ' ');
        while (t.Contains("  ", StringComparison.Ordinal))
            t = t.Replace("  ", " ", StringComparison.Ordinal);
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d.ToString(CultureInfo.InvariantCulture);
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.GetCultureInfo("ru-RU"), out var d2))
            return d2.ToString(CultureInfo.InvariantCulture);
        return t;
    }
}
