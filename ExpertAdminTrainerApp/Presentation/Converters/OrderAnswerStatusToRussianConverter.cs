using System.Globalization;
using System.Windows.Data;
using ExpertAdminTrainerApp.Domain;

namespace ExpertAdminTrainerApp.Presentation.Converters;

public sealed class OrderAnswerStatusToRussianConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is OrderAnswerStatus s ? OrderAnswerStatusLabels.Russian(s) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
