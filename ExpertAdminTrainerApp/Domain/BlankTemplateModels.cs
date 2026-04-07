using System.Text.Json.Serialization;

namespace ExpertAdminTrainerApp.Domain;

public class ZoneDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("fieldType")]
    public ZoneFieldType FieldType { get; set; }

    [JsonPropertyName("taskNumber")]
    public int TaskNumber { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("width")]
    public float Width { get; set; }

    [JsonPropertyName("height")]
    public float Height { get; set; }
}

public class BlankPageDefinition
{
    [JsonPropertyName("blankType")]
    public BlankType BlankType { get; set; }

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; } = 1;

    [JsonPropertyName("imagePath")]
    public string ImagePath { get; set; } = string.Empty;

    [JsonPropertyName("zones")]
    public List<ZoneDefinition> Zones { get; set; } = [];
}

public class BlankTemplateDefinition
{
    [JsonPropertyName("cnnId")]
    public int CnnId { get; set; }

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("option")]
    public int Option { get; set; }

    [JsonPropertyName("pages")]
    public List<BlankPageDefinition> Pages { get; set; } = [];
}
