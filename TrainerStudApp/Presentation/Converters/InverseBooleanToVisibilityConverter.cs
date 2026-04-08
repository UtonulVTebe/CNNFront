using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrainerStudApp.Presentation.Converters;

/// <summary>Скрывает элемент при <c>true</c>, показывает при <c>false</c> (для гостевых блоков при IsAuthenticated).</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
