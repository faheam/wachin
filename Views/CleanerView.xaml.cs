using System.Windows;
using System.Windows.Controls;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.Views;

public partial class CleanerView : UserControl
{
    private List<CleanerCategory> _categories = new();

    public CleanerView()
    {
        InitializeComponent();
        _categories = CleanerService.BuildCategories();
        CleanerList.ItemsSource = _categories;
    }

    private async void OnScan(object sender, RoutedEventArgs e)
    {
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Analizando archivos…";
        CleanBtn.IsEnabled = false;

        foreach (var cat in _categories.Where(c => c.SupportsSize))
        {
            StatusText.Text = $"Analizando: {cat.Name}…";
            var (size, count) = await CleanerService.ScanAsync(cat);
            cat.SizeBytes = size;
            cat.ItemCount = count;
        }

        // Recycle bin
        var recycle = _categories.FirstOrDefault(c => c.Id == "recycle");
        if (recycle != null)
        {
            var (size, count) = await Task.Run(() =>
            {
                // Reuse scan method or just query
                var cat = recycle;
                return CleanerService.ScanAsync(cat);
            });
            recycle.SizeBytes = size;
            recycle.ItemCount = (int)size; // item count is 0 for recycle, we show size
        }

        Progress.Visibility = Visibility.Collapsed;
        StatusText.Text = "";

        long total = _categories.Where(c => c.Selected).Sum(c => c.SizeBytes);
        int totalItems = _categories.Where(c => c.Selected).Sum(c => c.ItemCount);
        StatusText.Text = $"Listo para limpiar: {FormatSize(total)} ({_categories.Count(c => c.Selected)} categorías seleccionadas)";
        CleanBtn.IsEnabled = true;
        CleanerList.Items.Refresh();
    }

    private async void OnClean(object sender, RoutedEventArgs e)
    {
        var selected = _categories.Where(c => c.Selected && (c.SizeBytes > 0 || c.SupportsSize)).ToList();
        if (!selected.Any())
        {
            AppServices.ShowWarning("Nada seleccionado", "Seleccioná al menos una categoría para limpiar.");
            return;
        }

        Progress.Visibility = Visibility.Visible;
        CleanBtn.IsEnabled = false;

        long totalFreed = 0;
        int totalItems = 0;

        foreach (var cat in selected)
        {
            StatusText.Text = $"Limpiando: {cat.Name}…";
            var (freed, items, _) = await CleanerService.CleanAsync(cat);
            totalFreed += freed;
            totalItems += items;
            cat.SizeBytes = 0;
            cat.ItemCount = 0;
        }

        Progress.Visibility = Visibility.Collapsed;
        StatusText.Text = "";

        ResultBorder.Visibility = Visibility.Visible;
        ResultText.Text = $"✅ Limpieza completada: {FormatSize(totalFreed)} liberados en {totalItems} archivos.";

        CleanerList.Items.Refresh();
        CleanBtn.IsEnabled = false;
    }

    private static string FormatSize(long b) => b switch
    {
        0 => "0 B",
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{b / 1048576.0:F1} MB",
        _ => $"{b / 1073741824.0:F2} GB"
    };
}
