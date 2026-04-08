using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExpertAdminTrainerApp.Domain;
using ExpertAdminTrainerApp.Presentation.Controls;
using ExpertAdminTrainerApp.Presentation.ViewModels;

namespace ExpertAdminTrainerApp.Presentation.Views;

public partial class ConstructorView : UserControl
{
    public ConstructorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SubscribeVm(DataContext as BlankConstructorViewModel);
        Loaded += OnLoaded;
    }

    private BlankConstructorViewModel? Vm => DataContext as BlankConstructorViewModel;

    private BlankConstructorViewModel? _subscribedVm;

    private void SubscribeVm(BlankConstructorViewModel? vm)
    {
        if (ReferenceEquals(_subscribedVm, vm))
            return;
        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;
            _subscribedVm.ZonesListRefreshStarting -= OnZonesListRefreshStarting;
            _subscribedVm.ZonesListRefreshCompleted -= OnZonesListRefreshCompleted;
        }

        _subscribedVm = vm;
        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;
            _subscribedVm.ZonesListRefreshStarting += OnZonesListRefreshStarting;
            _subscribedVm.ZonesListRefreshCompleted += OnZonesListRefreshCompleted;
        }
    }

    private void OnZonesListRefreshStarting()
    {
        EditorCanvas.BeginBatchUpdate();
    }

    private void OnZonesListRefreshCompleted()
    {
        EditorCanvas.EndBatchUpdate();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeVm(DataContext as BlankConstructorViewModel);
        if (Vm is not null && Vm.Cnns.Count == 0)
            _ = Vm.LoadCnnListCommand.ExecuteAsync(null);
        MirrorSidebarAndGridSelection();
    }

    private bool _syncingSelectionFromVm;

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BlankConstructorViewModel.SelectedZone))
            return;
        Dispatcher.BeginInvoke(MirrorSidebarAndGridSelection);
    }

    private void MirrorSidebarAndGridSelection()
    {
        if (Vm is null)
            return;
        _syncingSelectionFromVm = true;
        try
        {
            if (ZonesSidebarList.SelectedItem != Vm.SelectedZone)
                ZonesSidebarList.SelectedItem = Vm.SelectedZone;
            if (ZonesOverviewGrid.SelectedItem != Vm.SelectedZone)
                ZonesOverviewGrid.SelectedItem = Vm.SelectedZone;
        }
        finally
        {
            _syncingSelectionFromVm = false;
        }
    }

    private void OnEditorZoneAdded(object sender, RoutedEventArgs e)
    {
        Vm?.OnZoneAdded();
    }

    private void OnEditorZoneSelected(object sender, RoutedEventArgs e)
    {
        if (sender is ZoneEditorCanvas canvas)
            Vm?.ApplyCanvasSelection(canvas.SelectedZone);
    }

    private void OnZoneMutationStarting(object sender, ZoneMutationStartingEventArgs e)
    {
        if (Vm is null) return;
        if (e.Kind is ZoneMutationKind.Stamp or ZoneMutationKind.DragStart or ZoneMutationKind.Delete)
            Vm.PushUndoSnapshot();
    }

    private void OnPendingPresetConsumed(object sender, RoutedEventArgs e)
    {
        Vm?.ClearPendingPreset();
    }

    private void OnPresetDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: ZonePresetTemplate preset })
            return;
        Vm?.SelectPresetCommand.Execute(preset);
    }

    private void OnZonesSidebarSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelectionFromVm || Vm is null)
            return;
        if (sender is ListBox { SelectedItem: ZoneDefinition z })
            Vm.SelectZoneFromSidebar(z);
    }

    private void OnZonesOverviewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelectionFromVm || Vm is null)
            return;
        if (sender is DataGrid { SelectedItem: ZoneDefinition z })
            Vm.SelectZoneFromSidebar(z);
    }
}
