using System.IO;
using System.Text.Json;
using System.Windows.Ink;

namespace ExpertAdminTrainerApp.Presentation.Controls;

internal static class ZoneInkAnswerCodec
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public sealed class Payload
    {
        public string? Text { get; set; }
        public string? InkIsf { get; set; }
    }

    public static bool TryParse(string? json, out string? text, out StrokeCollection strokes)
    {
        text = null;
        strokes = new StrokeCollection();
        if (string.IsNullOrEmpty(json))
            return true;

        if (string.Equals(json, "1", StringComparison.Ordinal))
            return true;

        var trimmed = json.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '{')
        {
            text = json;
            return true;
        }

        try
        {
            var p = JsonSerializer.Deserialize<Payload>(json, JsonOpts);
            if (p is null)
                return false;
            text = p.Text;
            if (!string.IsNullOrEmpty(p.InkIsf))
            {
                var bytes = Convert.FromBase64String(p.InkIsf);
                using var ms = new MemoryStream(bytes);
                strokes = new StrokeCollection(ms);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
