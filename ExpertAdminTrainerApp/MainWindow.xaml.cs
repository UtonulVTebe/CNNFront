using ExpertAdminTrainerApp.Presentation.ViewModels;
using Wpf.Ui.Controls;

namespace ExpertAdminTrainerApp;

public partial class MainWindow : FluentWindow
{
    public BlankConstructorViewModel ConstructorVm { get; }

    public MainWindow(MainViewModel viewModel, BlankConstructorViewModel constructorVm)
    {
        InitializeComponent();
        DataContext = viewModel;
        ConstructorVm = constructorVm;
    }
}
