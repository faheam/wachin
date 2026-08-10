using System.Windows;
using System.Windows.Controls;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.Views;

public partial class BloatwareView : UserControl
{
    public BloatwareView()
    {
        InitializeComponent();
        // Don't auto-load - wait for user to click Scan
    }

    private async void OnScan(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        ScanBtn.Content = "Escaneando...";
        StatusText.Text = "Buscando apps preinstaladas...";

        var apps = await Task.Run(() => BloatwareService.GetInstalled());
        var installed = apps.Where(a => a.Installed).ToList();

        BloatGrid.ItemsSource = installed;
        RemoveBtn.IsEnabled = installed.Count > 0;

        // Switch from empty state to results
        EmptyState.Visibility = Visibility.Collapsed;
        ResultsScroll.Visibility = Visibility.Visible;

        ScanBtn.IsEnabled = true;
        ScanBtn.Content = "Escanear de nuevo";
        StatusText.Text = installed.Count > 0
            ? $"{installed.Count} apps preinstaladas encontradas"
            : "No se encontraron apps preinstaladas";
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => OnScan(sender, e);

    private async void OnRemoveSingle(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is BloatApp app)
        {
            btn.IsEnabled = false;
            btn.Content = "Eliminando...";
            var (ok, msg) = await BloatwareService.RemoveAsync(app);
            if (ok)
            {
                app.Installed = false;
                AppServices.ShowSuccess(app.Name, msg);
                // Remove from list
                var items = BloatGrid.ItemsSource as List<BloatApp>;
                items?.Remove(app);
                BloatGrid.Items.Refresh();
                StatusText.Text = $"Se elimino {app.Name}";
            }
            else
            {
                btn.IsEnabled = true;
                btn.Content = "Eliminar";
                AppServices.ShowError("Error", msg);
            }
        }
    }

    private async void OnRemoveAll(object sender, RoutedEventArgs e)
    {
        var installed = BloatGrid.ItemsSource as List<BloatApp>;
        if (installed == null || !installed.Any()) return;

        var result = MessageBox.Show(
            $"Se eliminaran {installed.Count} apps preinstaladas.\n\nContinuar?",
            "Confirmar eliminacion", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        RemoveBtn.IsEnabled = false;
        int removed = 0;
        foreach (var app in installed.ToList())
        {
            StatusText.Text = $"Eliminando {app.Name}...";
            var (ok, _) = await BloatwareService.RemoveAsync(app);
            if (ok) { app.Installed = false; removed++; }
        }
        StatusText.Text = removed > 0
            ? $"{removed} apps eliminadas correctamente"
            : "No se pudo eliminar ninguna app";
        RemoveBtn.IsEnabled = false;
    }
}
