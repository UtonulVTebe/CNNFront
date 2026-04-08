using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using TrainerStudApp.Presentation.ViewModels;

namespace TrainerStudApp;

public partial class MainWindow
{
    public MainWindow(StudentMainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            if (viewModel.RestoreSessionCommand is IAsyncRelayCommand restoreCmd)
                await restoreCmd.ExecuteAsync(null);
        };
        viewModel.Exam.GradingCompleted += (_, _) =>
            Dispatcher.Invoke(() => { viewModel.SelectedNavIndex = 3; });
    }

    private async void NavList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not ListBox lb || !ReferenceEquals(lb, NavList))
            return;
        if (DataContext is not StudentMainViewModel vm || !vm.IsAuthenticated)
            return;
        if (lb.SelectedIndex != 4)
            return;
        if (vm.Orders.RefreshMineCommand is IAsyncRelayCommand refreshCmd)
            await refreshCmd.ExecuteAsync(null);
    }

    private async void OnProfileLoginClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not StudentMainViewModel vm) return;
        if (vm.LoginCommand is IAsyncRelayCommand<string?> cmd)
            await cmd.ExecuteAsync(ProfileLoginPwd.Password);
        ProfileLoginPwd.Clear();
    }

    private async void OnRegisterConfirmClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not StudentMainViewModel vm) return;
        if (vm.RegisterConfirmCommand is IAsyncRelayCommand<string?> cmd)
            await cmd.ExecuteAsync(RegisterPwd.Password);
        RegisterPwd.Clear();
    }

    private async void OnPasswordResetConfirmClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not StudentMainViewModel vm) return;
        if (vm.PasswordResetConfirmCommand is IAsyncRelayCommand<string?> cmd)
            await cmd.ExecuteAsync(ResetNewPwd.Password);
        ResetNewPwd.Clear();
    }

    private void OnBlankUndoInkClick(object sender, RoutedEventArgs e)
    {
        _ = FillCanvas.TryUndoLastInkStroke();
    }

    private void OnBlankInkEraserToggleClick(object sender, RoutedEventArgs e)
    {
        _ = FillCanvas.ToggleInkEraserMode();
    }
}
