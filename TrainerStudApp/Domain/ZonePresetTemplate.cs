namespace TrainerStudApp.Domain;

/// <summary>Описание пресета для вставки группы зон по клику на бланк.</summary>
public sealed class ZonePresetTemplate
{
    public const string CorrectionTaskNumberThenAnswerId = "correction_task_number_then_answer";

    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseFieldName { get; init; } = string.Empty;
    public string? FieldRole { get; init; }
    public int CellCount { get; init; } = 1;
    public ZoneFieldType FieldType { get; init; } = ZoneFieldType.ShortAnswer;
    public ZoneInputMode InputMode { get; init; } = ZoneInputMode.Cell;
    public ZoneValidationRules? Validation { get; init; }
    public int StartTaskNumberOffset { get; init; }

    public float? DefaultWidthPercent { get; init; }
    public float? DefaultHeightPercent { get; init; }

    public static IReadOnlyList<ZonePresetTemplate> Catalog { get; } =
    [
        new ZonePresetTemplate
        {
            Id = "region_code",
            DisplayName = "Код региона (2 цифры)",
            BaseFieldName = "код_региона",
            FieldRole = "region_code",
            CellCount = 2,
            FieldType = ZoneFieldType.Header,
            Validation = new ZoneValidationRules { MinLength = 2, MaxLength = 2, DigitsOnly = true }
        },
        new ZonePresetTemplate
        {
            Id = "subject_code",
            DisplayName = "Код предмета (2 цифры)",
            BaseFieldName = "код_предмета",
            FieldRole = "subject_code",
            CellCount = 2,
            FieldType = ZoneFieldType.Header,
            Validation = new ZoneValidationRules { MinLength = 2, MaxLength = 2, DigitsOnly = true }
        },
        new ZonePresetTemplate
        {
            Id = "subject_abbr",
            DisplayName = "Предмет (3 буквы)",
            BaseFieldName = "предмет",
            FieldRole = "subject_name",
            CellCount = 3,
            FieldType = ZoneFieldType.Header,
            Validation = new ZoneValidationRules { MinLength = 3, MaxLength = 3, LettersOnly = true }
        },
        new ZonePresetTemplate
        {
            Id = "ppe_code",
            DisplayName = "Код ППЭ (3 ячейки)",
            BaseFieldName = "код_ппэ",
            FieldRole = "ppe_code",
            CellCount = 3,
            FieldType = ZoneFieldType.Header,
            Validation = new ZoneValidationRules { DigitsOnly = true, MinLength = 3, MaxLength = 3 }
        },
        new ZonePresetTemplate
        {
            Id = "org_code",
            DisplayName = "Код ОО",
            BaseFieldName = "код_оо",
            FieldRole = "org_code",
            CellCount = 1,
            FieldType = ZoneFieldType.Header,
            InputMode = ZoneInputMode.Text
        },
        new ZonePresetTemplate
        {
            Id = "class_number",
            DisplayName = "Класс: номер",
            BaseFieldName = "класс_номер",
            FieldRole = "class_number",
            CellCount = 1,
            FieldType = ZoneFieldType.Header,
            Validation = new ZoneValidationRules { DigitsOnly = true, MaxLength = 2 }
        },
        new ZonePresetTemplate
        {
            Id = "class_letter",
            DisplayName = "Класс: буква",
            BaseFieldName = "класс_буква",
            FieldRole = "class_letter",
            CellCount = 1,
            FieldType = ZoneFieldType.Header,
            Validation = new ZoneValidationRules { LettersOnly = true, MaxLength = 2 }
        },
        new ZonePresetTemplate
        {
            Id = "auditorium",
            DisplayName = "Номер аудитории",
            BaseFieldName = "аудитория",
            FieldRole = "auditorium",
            CellCount = 1,
            FieldType = ZoneFieldType.Header
        },
        new ZonePresetTemplate
        {
            Id = "exam_date",
            DisplayName = "Дата ЕГЭ (ДД-ММ-ГГ)",
            BaseFieldName = "дата_егэ",
            FieldRole = "exam_date",
            CellCount = 1,
            FieldType = ZoneFieldType.Header,
            InputMode = ZoneInputMode.Text,
            Validation = new ZoneValidationRules { Pattern = @"^\d{2}-\d{2}-\d{2}$" }
        },
        new ZonePresetTemplate
        {
            Id = "surname",
            DisplayName = "Фамилия",
            BaseFieldName = "фамилия",
            FieldRole = "surname",
            CellCount = 1,
            FieldType = ZoneFieldType.Header,
            InputMode = ZoneInputMode.Text
        },
        new ZonePresetTemplate
        {
            Id = "given_name",
            DisplayName = "Имя",
            BaseFieldName = "имя",
            FieldRole = "given_name",
            CellCount = 1,
            FieldType = ZoneFieldType.Header,
            InputMode = ZoneInputMode.Text
        },
        new ZonePresetTemplate
        {
            Id = "patronymic",
            DisplayName = "Отчество",
            BaseFieldName = "отчество",
            FieldRole = "patronymic",
            CellCount = 1,
            FieldType = ZoneFieldType.Header,
            InputMode = ZoneInputMode.Text
        },
        new ZonePresetTemplate
        {
            Id = "passport",
            DisplayName = "Паспорт (серия номер, X — любой символ в маске)",
            BaseFieldName = "паспорт",
            FieldRole = "passport_id",
            CellCount = 1,
            FieldType = ZoneFieldType.FreeForm,
            InputMode = ZoneInputMode.Text,
            Validation = new ZoneValidationRules { Mask = "XXXX XXXXXX", Pattern = @"^(\d{4}\s?\d{6}|[\dX]{4}\s?[\dX]{6})$" }
        },
        new ZonePresetTemplate
        {
            Id = "sheet2_number",
            DisplayName = "Бланк ответов №2 (лист 2)",
            BaseFieldName = "бланк2_номер",
            FieldRole = "answer_sheet2_number",
            CellCount = 1,
            FieldType = ZoneFieldType.Header
        },
        new ZonePresetTemplate
        {
            Id = "sheet_number",
            DisplayName = "Номер листа",
            BaseFieldName = "лист",
            FieldRole = "sheet_number",
            CellCount = 1,
            FieldType = ZoneFieldType.Header
        },
        new ZonePresetTemplate
        {
            Id = "short_answer_row",
            DisplayName = "Ряд кратких ответов (кол-во из панели)",
            BaseFieldName = "ответ",
            FieldRole = null,
            CellCount = -1,
            FieldType = ZoneFieldType.ShortAnswer
        },
        new ZonePresetTemplate
        {
            Id = "correction_row",
            DisplayName = "Ряд исправлений (кол-во из панели)",
            BaseFieldName = "исправление",
            FieldRole = null,
            CellCount = -1,
            FieldType = ZoneFieldType.Correction
        },
        new ZonePresetTemplate
        {
            Id = CorrectionTaskNumberThenAnswerId,
            DisplayName = "Исправление: номер задания (2 яч.) + ответ (кол-во из панели)",
            BaseFieldName = "исправление",
            FieldRole = null,
            CellCount = 1,
            FieldType = ZoneFieldType.Correction
        },
        new ZonePresetTemplate
        {
            Id = "drawing_area",
            DisplayName = "Зона рисунка (матем./физ.)",
            BaseFieldName = "рисунок",
            FieldRole = null,
            CellCount = 1,
            FieldType = ZoneFieldType.Drawing,
            InputMode = ZoneInputMode.Drawing,
            Validation = null,
            DefaultWidthPercent = 72f,
            DefaultHeightPercent = 55f
        },
        new ZonePresetTemplate
        {
            Id = "free_text_area",
            DisplayName = "Текстовое поле (развёрнутый)",
            BaseFieldName = "текст",
            FieldRole = null,
            CellCount = 1,
            FieldType = ZoneFieldType.LongAnswer,
            InputMode = ZoneInputMode.Text,
            DefaultWidthPercent = 88f,
            DefaultHeightPercent = 72f
        },
        new ZonePresetTemplate
        {
            Id = "expanded_text_and_drawing",
            DisplayName = "Развёрнутый ответ + рисунок (одна зона)",
            BaseFieldName = "развернутый",
            FieldRole = null,
            CellCount = 1,
            FieldType = ZoneFieldType.LongAnswer,
            InputMode = ZoneInputMode.TextAndDrawing,
            DefaultWidthPercent = 88f,
            DefaultHeightPercent = 72f
        }
    ];
}
