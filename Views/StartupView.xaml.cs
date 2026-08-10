using System.Windows;
using System.Windows.Controls;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.Views;

public partial class StartupView : UserControl
{
    public StartupView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadItemsAsync();
    }

    private async Task LoadItemsAsync()
    {
        await Task.Delay(50);
        List.ItemsSource = await Task.Run(() => StartupService.GetAll(AppServices.State));
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadItemsAsync();

    private async void OnToggle(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is StartupItem item)
        {
            await Task.Run(() =>
            {
                if (item.IsEnabled)
                    StartupService.Disable(item, AppServices.State);
                else
                    StartupService.Enable(item, AppServices.State);
            });
            await LoadItemsAsync();
        }
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is StartupItem item)
        {
            var result = MessageBox.Show(
                $"¿Eliminar \"{item.Name}\" permanentemente?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await Task.Run(() => StartupService.Delete(item, AppServices.State));
                await LoadItemsAsync();
            }
        }
    }
}
