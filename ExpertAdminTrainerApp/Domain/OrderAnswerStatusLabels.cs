namespace ExpertAdminTrainerApp.Domain;

/// <summary>Человекочитаемые подписи статуса заказа (UI).</summary>
public static class OrderAnswerStatusLabels
{
    public static string Russian(OrderAnswerStatus s) => s switch
    {
        OrderAnswerStatus.NoCheck => "Без проверки",
        OrderAnswerStatus.PaymentInProgress => "Оплата",
        OrderAnswerStatus.QueueForCheck => "В очереди на проверку",
        OrderAnswerStatus.Checking => "На проверке",
        OrderAnswerStatus.Checked => "Проверено",
        OrderAnswerStatus.RejectedByExpert => "Отклонено экспертом",
        _ => s.ToString()
    };
}
