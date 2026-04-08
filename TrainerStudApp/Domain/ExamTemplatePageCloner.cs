namespace TrainerStudApp.Domain;

/// <summary>
/// Копии страниц шаблона для сеанса экзамена: новые Id зон, порядок страниц.
/// </summary>
public static class ExamTemplatePageCloner
{
    public static ZoneDefinition CloneZoneWithNewId(ZoneDefinition z)
    {
        var c = ZoneDefinitionCopy.Clone(z);
        c.Id = Guid.NewGuid().ToString("N");
        return c;
    }

    public static BlankPageDefinition ClonePageWithNewZoneIds(BlankPageDefinition source) => new()
    {
        BlankType = source.BlankType,
        PageNumber = source.PageNumber,
        ImagePath = source.ImagePath,
        Zones = source.Zones.Select(CloneZoneWithNewId).ToList()
    };

    /// <summary>
    /// Рабочая копия шаблона: сортировка Регистрация → №1 → №2, новые Id у всех зон.
    /// </summary>
    public static BlankTemplateDefinition CreateExamWorkingCopy(
        BlankTemplateDefinition source,
        out BlankPageDefinition? answerSheet2Prototype,
        out int baseAnswerSheet2PageCount)
    {
        var sorted = source.Pages
            .OrderBy(p => (int)p.BlankType)
            .ThenBy(p => p.PageNumber)
            .Select(ClonePageWithNewZoneIds)
            .ToList();

        var as2 = sorted.Where(p => p.BlankType == BlankType.AnswerSheet2).ToList();
        baseAnswerSheet2PageCount = as2.Count;
        answerSheet2Prototype = as2.Count > 0
            ? ClonePageWithNewZoneIds(as2[^1])
            : null;

        return new BlankTemplateDefinition
        {
            CnnId = source.CnnId,
            Subject = source.Subject,
            Option = source.Option,
            AutoAnswers = [.. source.AutoAnswers],
            Pages = sorted
        };
    }
}
