using System.Globalization;
using System.Windows.Data;
using TrainerStudApp.Domain;

namespace TrainerStudApp.Presentation.Converters;

public sealed class OrderAnswerStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is OrderAnswerStatus s ? Format(s) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public static string Format(OrderAnswerStatus s) => s switch
    {
        OrderAnswerStatus.NoCheck => "Черновик",
        OrderAnswerStatus.PaymentInProgress => "Ожидание оплаты",
        OrderAnswerStatus.QueueForCheck => "В очереди на проверку",
        OrderAnswerStatus.Checking => "Проверяется",
        OrderAnswerStatus.Checked => "Проверено",
        OrderAnswerStatus.Rejected => "Отклонено экспертом",
        _ => s.ToString()
    };
}
