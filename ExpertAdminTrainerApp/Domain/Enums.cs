namespace ExpertAdminTrainerApp.Domain;

public enum MaterialKind
{
    Kim = 0,
    Criteria = 1,
    Blanks = 2,
    Other = 3
}

public enum OrderAnswerStatus
{
    NoCheck = 0,
    PaymentInProgress = 1,
    QueueForCheck = 2,
    Checking = 3,
    Checked = 4,
    /// <summary>Отклонено экспертом (сервер: RejectedByExpert).</summary>
    RejectedByExpert = 5
}

public enum PlatformTransactionType
{
    PaymentForCheck = 0,
    CreditForCheck = 1,
    Withdrawal = 2
}

public enum AnnotationFieldType
{
    ShortAnswer = 0,
    LongAnswer = 1,
    Other = 2
}

public enum BlankType
{
    Registration = 0,
    AnswerSheet1 = 1,
    AnswerSheet2 = 2
}

public enum ZoneFieldType
{
    Header = 0,
    ShortAnswer = 1,
    LongAnswer = 2,
    FreeForm = 3,
    Correction = 4,
    /// <summary>Сетка ячеек (бланк №2 и т.п.).</summary>
    CellGrid = 5,
    /// <summary>Зона для рукописного/графического ответа (canvas на клиенте).</summary>
    Drawing = 6
}

/// <summary>Способ ввода в зоне на клиенте проверки.</summary>
public enum ZoneInputMode
{
    /// <summary>По ячейкам (по умолчанию для старых шаблонов).</summary>
    Cell = 0,
    /// <summary>Свободный текст.</summary>
    Text = 1,
    /// <summary>Рисование (математика/физика).</summary>
    Drawing = 2,
    /// <summary>Текст и рисунок в одной зоне (бланк №2).</summary>
    TextAndDrawing = 3
}
