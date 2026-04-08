using TrainerStudApp.Domain;

namespace TrainerStudApp.Presentation.ViewModels;

internal static class ExamAnswerKeyHelper
{
    public static string Key(int pageIndex, string zoneId) => $"{pageIndex}|{zoneId}";

    /// <summary>Ключи в том же порядке, что и <see cref="Controls.BlankFillCanvas"/> (доли зоны в процентах).</summary>
    public static IEnumerable<string> EnumerateKeysForZone(ZoneDefinition zone, int pageIndex)
    {
        var baseKey = Key(pageIndex, zone.Id);

        if (!BlankFillCanvasStatic.WantsInteractiveInput(zone))
            yield break;

        switch (zone.FieldType)
        {
            case ZoneFieldType.Drawing when zone.InputMode == ZoneInputMode.Drawing:
                yield return baseKey;
                yield break;
            case ZoneFieldType.CellGrid when zone.InputMode != ZoneInputMode.Drawing:
                foreach (var k in CellGridKeys(baseKey, zone))
                    yield return k;
                yield break;
            default:
                if (zone.InputMode == ZoneInputMode.Text ||
                    (zone.InputMode == ZoneInputMode.Drawing && zone.FieldType != ZoneFieldType.Drawing))
                {
                    yield return baseKey;
                    yield break;
                }

                if (zone.InputMode == ZoneInputMode.Cell && zone.Width >= zone.Height * 1.4f)
                {
                    var cellWPct = zone.Height * 0.85f;
                    var n = Math.Max(1, (int)Math.Floor(zone.Width / cellWPct));
                    for (var i = 0; i < n; i++)
                        yield return $"{baseKey}|{i}";
                }
                else
                    yield return baseKey;
                break;
        }
    }

    private static IEnumerable<string> CellGridKeys(string baseKey, ZoneDefinition zone)
    {
        var cellPct = Math.Max(0.5f, Math.Min(zone.Width, zone.Height) / 8f);
        var cols = Math.Max(1, (int)Math.Floor(zone.Width / cellPct));
        var rows = Math.Max(1, (int)Math.Floor(zone.Height / cellPct));
        var idx = 0;
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                yield return $"{baseKey}|{idx++}";
    }
}

internal static class BlankFillCanvasStatic
{
    public static bool WantsInteractiveInput(ZoneDefinition z) =>
        z.FieldType is ZoneFieldType.Header
            or ZoneFieldType.ShortAnswer
            or ZoneFieldType.LongAnswer
            or ZoneFieldType.FreeForm
            or ZoneFieldType.Correction
            or ZoneFieldType.CellGrid
            or ZoneFieldType.Drawing;
}
