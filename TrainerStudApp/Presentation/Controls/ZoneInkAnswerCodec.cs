using System.IO;
using System.Text.Json;
using System.Windows.Ink;

namespace TrainerStudApp.Presentation.Controls;

/// <summary>Сериализация текста и штрихов Ink (ISF, Base64) в одну строку для <see cref="IZoneAnswerSink"/>.</summary>
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

    public static string Serialize(string? text, StrokeCollection strokes)
    {
        string? inkB64 = null;
        if (strokes.Count > 0)
        {
            using var ms = new MemoryStream();
            strokes.Save(ms);
            inkB64 = Convert.ToBase64String(ms.ToArray());
        }

        var hasText = !string.IsNullOrEmpty(text);
        if (!hasText && inkB64 is null)
            return string.Empty;

        return JsonSerializer.Serialize(new Payload { Text = hasText ? text : null, InkIsf = inkB64 }, JsonOpts);
    }

    /// <summary>Разбор ответа зоны: JSON с полями text/inkIsf, устаревший маркер «1», или обычная строка текста.</summary>
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
