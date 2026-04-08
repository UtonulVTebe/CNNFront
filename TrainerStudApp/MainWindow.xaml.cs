using System.Windows;
using TrainerStudApp.Presentation.ViewModels;

namespace TrainerStudApp;

public partial class MainWindow
{
    public MainWindow(StudentMainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudentMainViewModel vm)
            await vm.LoginCommand.ExecuteAsync(PwdBox.Password);
    }
}
