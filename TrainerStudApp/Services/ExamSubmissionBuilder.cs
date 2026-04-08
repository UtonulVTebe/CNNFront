using System.Text.Json;
using TrainerStudApp.Domain;
using TrainerStudApp.Presentation.ViewModels;

namespace TrainerStudApp.Services;

public static class ExamSubmissionBuilder
{
    public static ExamSubmissionDocument Build(ExamSessionViewModel session)
    {
        var template = session.CurrentTemplate
            ?? throw new InvalidOperationException("Нет активного шаблона бланков.");

        var answers = session.ExportAnswersDictionary();

        var autoByTask = session.BuildAnswersByTaskNumber();
        var autoPart = autoByTask
            .Select(kv => new AnswerAutoPartItem
            {
                TaskId = kv.Key,
                Answer = kv.Value,
                Points = 0
            })
            .ToList();

        var expPart = BuildExpPart(template, answers);

        return new ExamSubmissionDocument
        {
            SchemaVersion = ExamSubmissionDocument.CurrentSchemaVersion,
            CnnId = template.CnnId,
            Template = template,
            Answers = new Dictionary<string, string>(answers, StringComparer.Ordinal),
            AnswerPayload = new AnswerPayload
            {
                Meta = new AnswerPayloadMeta { CreatedAt = DateTime.UtcNow },
                AutoPart = autoPart,
                ExpPart = expPart
            }
        };
    }

    private static List<AnswerExpPartItem> BuildExpPart(BlankTemplateDefinition template,
        IReadOnlyDictionary<string, string> answers)
    {
        var list = new List<AnswerExpPartItem>();

        for (var pi = 0; pi < template.Pages.Count; pi++)
        {
            var page = template.Pages[pi];
            foreach (var zone in page.Zones)
            {
                if (zone.TaskNumber <= 0)
                    continue;

                if (zone.FieldType is not (ZoneFieldType.LongAnswer or ZoneFieldType.Drawing))
                    continue;

                var key = ExamAnswerKeyHelper.Key(pi, zone.Id);
                answers.TryGetValue(key, out var raw);

                if (!TryGetExpSummary(zone.FieldType, raw, out var summary))
                    continue;

                list.Add(new AnswerExpPartItem
                {
                    TaskId = zone.TaskNumber,
                    Type = ExpPartAnswerType.Text,
                    Answer = summary,
                    Points = 0
                });
            }
        }

        return list;
    }

    private static bool TryGetExpSummary(ZoneFieldType fieldType, string? raw, out string summary)
    {
        summary = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (string.Equals(raw, "1", StringComparison.Ordinal))
        {
            summary = "Рисунок";
            return fieldType == ZoneFieldType.Drawing;
        }

        var t = raw.TrimStart();
        if (t.Length > 0 && t[0] == '{')
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                string? text = null;
                if (root.TryGetProperty("text", out var te) && te.ValueKind == JsonValueKind.String)
                    text = te.GetString();

                var hasInk = root.TryGetProperty("inkIsf", out var inkEl)
                    && inkEl.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(inkEl.GetString());

                if (!string.IsNullOrWhiteSpace(text) && hasInk)
                {
                    summary = text.Trim() + " [+ рисунок на бланке]";
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    summary = text.Trim();
                    return true;
                }

                if (hasInk)
                {
                    summary = "Рисунок";
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        if (fieldType == ZoneFieldType.LongAnswer)
        {
            summary = raw.Trim();
            return summary.Length > 0;
        }

        return false;
    }
}
