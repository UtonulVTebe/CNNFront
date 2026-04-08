using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrainerStudApp.Domain;

/// <summary>Снимок сессии экзамена для проверки экспертом: шаблон с Id зон + ответы + сводный <see cref="AnswerPayload"/>.</summary>
public sealed class ExamSubmissionDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public int CnnId { get; set; }

    public BlankTemplateDefinition Template { get; set; } = null!;

    public Dictionary<string, string> Answers { get; set; } = new(StringComparer.Ordinal);

    public AnswerPayload? AnswerPayload { get; set; }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new ExpPartTypeJsonConverter()
        }
    };

    public static string Serialize(ExamSubmissionDocument doc) =>
        JsonSerializer.Serialize(doc, JsonOptions);

    public static ExamSubmissionDocument? Deserialize(string json) =>
        JsonSerializer.Deserialize<ExamSubmissionDocument>(json, JsonOptions);
}
