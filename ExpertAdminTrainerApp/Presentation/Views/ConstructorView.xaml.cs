using System.Windows;
using System.Windows.Controls;
using ExpertAdminTrainerApp.Presentation.Controls;
using ExpertAdminTrainerApp.Presentation.ViewModels;

namespace ExpertAdminTrainerApp.Presentation.Views;

public partial class ConstructorView : UserControl
{
    public ConstructorView() => InitializeComponent();

    private BlankConstructorViewModel? Vm => DataContext as BlankConstructorViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Vm is not null && Vm.Cnns.Count == 0)
            _ = Vm.LoadCnnListCommand.ExecuteAsync(null);
    }

    private void OnEditorZoneAdded(object sender, RoutedEventArgs e)
    {
        Vm?.OnZoneAdded();
    }

    private void OnEditorZoneSelected(object sender, RoutedEventArgs e)
    {
        if (sender is ZoneEditorCanvas canvas)
            Vm?.OnZoneSelected(canvas.SelectedZone);
    }
}
