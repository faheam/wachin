using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wachin.Services;

namespace Wachin.Views;

public partial class NotebookView : UserControl
{
    public NotebookView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        int current = await Task.Run(NotebookService.GetCurrentThrottleMax);
        if (current == 99)
        {
            StatusText.Text = "Modo Gaming / Frío activo (CPU al 99%)";
            StatusBadge.Text = "99%";
            StatusBadge.Foreground = ResourceBrush.Safe("Secondary", Brushes.Gray);
        }
        else if (current == 100)
        {
            StatusText.Text = "Rendimiento normal (CPU al 100%)";
            StatusBadge.Text = "100%";
            StatusBadge.Foreground = ResourceBrush.Safe("Success", Brushes.Gray);
        }
        else
        {
            StatusText.Text = "No se pudo leer el valor actual";
            StatusBadge.Text = "--";
            StatusBadge.Foreground = ResourceBrush.Safe("TextSecondary", Brushes.Gray);
        }
    }

    private async void OnApplyCool(object sender, RoutedEventArgs e)
    {
        await ApplyAsync(99, "Modo Gaming / Frío");
    }

    private async void OnApplyFull(object sender, RoutedEventArgs e)
    {
        await ApplyAsync(100, "Rendimiento Normal");
    }

    private async Task ApplyAsync(int percent, string mode)
    {
        CoolBtn.IsEnabled = false;
        FullBtn.IsEnabled = false;
        try
        {
            var (ok, msg) = await NotebookService.SetThrottleMaxAsync(percent);
            if (ok)
            {
                AppServices.ShowSuccess("¡Pedido!", msg);
                await RefreshStatusAsync();
            }
            else
            {
                AppServices.ShowError("Error", $"No se pudo aplicar {mode}: {msg}");
            }
        }
        catch (Exception ex)
        {
            AppServices.ShowError("Error inesperado", ex.Message);
        }
        finally
        {
            CoolBtn.IsEnabled = true;
            FullBtn.IsEnabled = true;
        }
    }
}
