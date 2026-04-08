using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ExpertAdminTrainerApp.Presentation.Controls;

[ContentProperty(nameof(InnerContent))]
public partial class UnderlineFieldChrome : UserControl
{
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid),
        typeof(bool),
        typeof(UnderlineFieldChrome),
        new PropertyMetadata(false));

    public static readonly DependencyProperty InnerContentProperty = DependencyProperty.Register(
        nameof(InnerContent),
        typeof(object),
        typeof(UnderlineFieldChrome),
        new PropertyMetadata(null));

    public UnderlineFieldChrome() => InitializeComponent();

    public bool IsInvalid
    {
        get => (bool)GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }

    public object? InnerContent
    {
        get => GetValue(InnerContentProperty);
        set => SetValue(InnerContentProperty, value);
    }
}
