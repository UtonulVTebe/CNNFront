using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ExpertAdminTrainerApp.Presentation.ViewModels;

namespace ExpertAdminTrainerApp.Presentation.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is not MainViewModel vm) return;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        Unloaded += (_, _) => vm.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (e.PropertyName == nameof(MainViewModel.SelectedUser))
        {
            AdminPasswordBox.Password = string.Empty;
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.AdminEditUserPassword) && string.IsNullOrEmpty(vm.AdminEditUserPassword))
            AdminPasswordBox.Password = string.Empty;
    }

    private void AdminPassword_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && DataContext is MainViewModel vm)
            vm.AdminEditUserPassword = pb.Password;
    }
}
