using System;
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

        // MainWindow — Transient: при повторном входе после выхода иначе накапливались бы подписки на singleton Exam.
        EventHandler onGradingCompleted = (_, _) =>
            Dispatcher.Invoke(() => { viewModel.SelectedNavIndex = 2; });
        viewModel.Exam.GradingCompleted += onGradingCompleted;
        Closed += (_, _) => viewModel.Exam.GradingCompleted -= onGradingCompleted;
    }

    private async void NavList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not ListBox lb || !ReferenceEquals(lb, NavList))
            return;
        if (DataContext is not StudentMainViewModel vm || !vm.IsAuthenticated)
            return;
        if (lb.SelectedIndex != 3)
            return;
        if (vm.Orders.RefreshMineCommand is IAsyncRelayCommand refreshCmd)
            await refreshCmd.ExecuteAsync(null);
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
