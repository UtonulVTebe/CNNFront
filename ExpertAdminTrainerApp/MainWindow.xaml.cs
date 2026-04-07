using System.Windows;
using Wpf.Ui.Controls;

namespace ExpertAdminTrainerApp
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnDemoDialogClick(object sender, RoutedEventArgs e)
        {
            var text = string.IsNullOrWhiteSpace(DemoSearchBox.Text)
                ? "(поле пустое)"
                : DemoSearchBox.Text;
            System.Windows.MessageBox.Show(
                $"Текст в поле ввода: {text}",
                "WPF UI — демо",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }
}
