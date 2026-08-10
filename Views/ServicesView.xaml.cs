using System.Windows;
using System.Windows.Controls;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.Views;

public partial class ServicesView : UserControl
{
    private List<ServiceItem> _all = new();

    public ServicesView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadServicesAsync();
    }

    private async Task LoadServicesAsync()
    {
        try
        {
            _all = await Task.Run(() => ServiceManagerService.GetAll(AppServices.State));
        }
        catch (Exception ex)
        {
            _all = new List<ServiceItem>();
            AppServices.ShowError("Error", $"No se pudieron cargar los servicios: {ex.Message}");
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (ServiceList == null) return;

        bool recommendedOnly = RecommendedToggle?.IsChecked == true;
        string query = SearchBox?.Text?.ToLowerInvariant() ?? "";

        var filtered = _all.AsEnumerable();
        if (recommendedOnly) filtered = filtered.Where(s => s.IsRecommended);
        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(s =>
                s.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        ServiceList.ItemsSource = filtered.ToList();
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();
    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadServicesAsync();

    private async void OnDisable(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ServiceItem item)
        {
            var (ok, msg) = await Task.Run(() => ServiceManagerService.Disable(item, AppServices.State));
            if (ok)
            {
                AppServices.ShowSuccess("Servicio", msg);
                ApplyFilter();
            }
            else AppServices.ShowError("Error", msg);
        }
    }

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ServiceItem item)
        {
            var (ok, msg) = await Task.Run(() => ServiceManagerService.Restore(item, AppServices.State));
            if (ok)
            {
                AppServices.ShowSuccess("Servicio", msg);
                ApplyFilter();
            }
            else AppServices.ShowError("Error", msg);
        }
    }

}
