using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrainerStudApp.Presentation.Converters;

/// <summary>Visible, если хотя бы одно из переданных значений — true; иначе Collapsed.</summary>
public sealed class AnyTrueToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        foreach (var v in values)
        {
            if (v is true)
                return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
