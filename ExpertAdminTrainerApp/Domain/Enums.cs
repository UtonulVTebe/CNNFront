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
    Rejected = 5
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
    Correction = 4
}
