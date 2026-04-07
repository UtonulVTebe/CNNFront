using ExpertAdminTrainerApp.Presentation.ViewModels;
using System.Windows;
using Wpf.Ui.Controls;

namespace ExpertAdminTrainerApp;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public BlankConstructorViewModel ConstructorVm { get; }

    public MainWindow(MainViewModel viewModel, BlankConstructorViewModel constructorVm)
    {
        InitializeComponent();
        _viewModel = viewModel;
        ConstructorVm = constructorVm;
        DataContext = _viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.RestoreSessionCommand.ExecuteAsync(null);
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PwdBox.Password;
        await _viewModel.LoginCommand.ExecuteAsync(null);
    }
}
