using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpertAdminTrainerApp.Domain;

public class AnswerPayload
{
    [JsonPropertyName("Meta")]
    public AnswerPayloadMeta? Meta { get; set; }

    [JsonPropertyName("Auto_Part")]
    public List<AnswerAutoPartItem> AutoPart { get; set; } = [];

    [JsonPropertyName("Exp_Part")]
    public List<AnswerExpPartItem> ExpPart { get; set; } = [];
}

public class AnswerPayloadMeta
{
    [JsonPropertyName("Created_At")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("Checking_Since")]
    public DateTime? CheckingSince { get; set; }

    [JsonPropertyName("AP_Points")]
    public int? ApPoints { get; set; }

    [JsonPropertyName("EP_Points")]
    public int? EpPoints { get; set; }
}

public class AnswerAutoPartItem
{
    [JsonPropertyName("Task_Id")]
    public int TaskId { get; set; }

    [JsonPropertyName("Answer")]
    public string Answer { get; set; } = string.Empty;

    [JsonPropertyName("Points")]
    public int Points { get; set; }
}

public enum ExpPartAnswerType
{
    Text,
    Pdf
}

public sealed class ExpPartTypeJsonConverter : JsonConverter<ExpPartAnswerType>
{
    public override ExpPartAnswerType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected string for Type.");
        }

        var raw = reader.GetString();
        return string.Equals(raw, "PDF", StringComparison.OrdinalIgnoreCase)
            ? ExpPartAnswerType.Pdf
            : ExpPartAnswerType.Text;
    }

    public override void Write(Utf8JsonWriter writer, ExpPartAnswerType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value == ExpPartAnswerType.Pdf ? "PDF" : "Text");
    }
}

public class AnswerExpPartItem
{
    [JsonPropertyName("Task_Id")]
    public int TaskId { get; set; }

    [JsonPropertyName("Type")]
    [JsonConverter(typeof(ExpPartTypeJsonConverter))]
    public ExpPartAnswerType Type { get; set; }

    [JsonPropertyName("Answer")]
    public string Answer { get; set; } = string.Empty;

    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("Comment_From_Expert")]
    public string? CommentFromExpert { get; set; }
}
