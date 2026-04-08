namespace TrainerStudApp.Domain;

public static class ZoneDefinitionCopy
{
    public static ZoneDefinition Clone(ZoneDefinition z) => new()
    {
        Id = z.Id,
        FieldName = z.FieldName,
        FieldType = z.FieldType,
        TaskNumber = z.TaskNumber,
        X = z.X,
        Y = z.Y,
        Width = z.Width,
        Height = z.Height,
        GroupId = z.GroupId,
        FieldRole = z.FieldRole,
        InputMode = z.InputMode,
        Validation = CloneValidation(z.Validation)
    };

    public static ZoneValidationRules? CloneValidation(ZoneValidationRules? v)
    {
        if (v is null) return null;
        return new ZoneValidationRules
        {
            MinLength = v.MinLength,
            MaxLength = v.MaxLength,
            Pattern = v.Pattern,
            Mask = v.Mask,
            DigitsOnly = v.DigitsOnly,
            LettersOnly = v.LettersOnly
        };
    }

    public static List<ZoneDefinition> CloneList(IEnumerable<ZoneDefinition> zones) =>
        zones.Select(Clone).ToList();
}
