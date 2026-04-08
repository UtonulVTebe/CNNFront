using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrainerStudApp.Presentation.Converters;

/// <summary><see cref="Visibility.Visible"/> если целое значение совпадает с <c>ConverterParameter</c> (строка с числом).</summary>
public sealed class IndexMatchToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int idx || parameter is not string s || !int.TryParse(s.Trim(), out var expected))
            return Visibility.Collapsed;
        return idx == expected ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
