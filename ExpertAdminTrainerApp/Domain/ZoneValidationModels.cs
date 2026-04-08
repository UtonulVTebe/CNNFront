using System.Text.Json.Serialization;

namespace ExpertAdminTrainerApp.Domain;

/// <summary>Правила локальной проверки значения зоны (клиент).</summary>
public sealed class ZoneValidationRules
{
    [JsonPropertyName("minLength")]
    public int? MinLength { get; set; }

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }

    /// <summary>Regex .NET; для паспорта можно задать отдельно или использовать <see cref="Mask"/>.</summary>
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    /// <summary>Упрощённая маска: цифра = цифра, X/x = любой символ.</summary>
    [JsonPropertyName("mask")]
    public string? Mask { get; set; }

    [JsonPropertyName("digitsOnly")]
    public bool? DigitsOnly { get; set; }

    [JsonPropertyName("lettersOnly")]
    public bool? LettersOnly { get; set; }
}

/// <summary>Эталон для автопроверки по номеру задания (краткий ответ).</summary>
public sealed class AutoAnswerEntry
{
    [JsonPropertyName("taskId")]
    public int TaskId { get; set; }

    [JsonPropertyName("answer")]
    public string Answer { get; set; } = string.Empty;
}
