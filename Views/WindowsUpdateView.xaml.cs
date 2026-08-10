using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wachin.Services;

namespace Wachin.Views;

public partial class WindowsUpdateView : UserControl
{
    public WindowsUpdateView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadHistoryAsync();
        await CheckPendingAsync();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var updates = await WindowsUpdateService.SearchUpdatesAsync();
            HistoryList.ItemsSource = updates;
            HistoryCountText.Text = $"{updates.Count} actualizaciones instaladas recientemente";
        }
        catch (Exception ex)
        {
            ShowStatus($"Error al cargar historial: {ex.Message}", false);
        }
    }

    private async Task CheckPendingAsync()
    {
        try
        {
            PendingCountText.Text = "Buscando actualizaciones pendientes...";
            var pending = await WindowsUpdateService.CheckPendingUpdatesAsync();

            var items = new List<object>();
            foreach (var item in pending)
            {
                var parts = item.Split('|');
                items.Add(new
                {
                    Title = parts.Length > 0 ? parts[0].Trim() : "Actualizacion",
                    Description = parts.Length > 1 ? parts[1].Trim() : "",
                    Size = "Pendiente"
                });
            }

            PendingList.ItemsSource = items;
            PendingCountText.Text = items.Count > 0
                ? $"{items.Count} actualizaciones pendientes"
                : "No hay actualizaciones pendientes";
        }
        catch
        {
            PendingCountText.Text = "No se pudieron buscar actualizaciones";
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        ShowStatus("Buscando actualizaciones...", true);
        var (ok, msg) = await WindowsUpdateService.CheckNowAsync();
        ShowStatus(msg, ok);
        if (ok) await CheckPendingAsync();
    }

    private async void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        await WindowsUpdateService.OpenUpdateSettingsAsync();
    }

    private async void RunSFC_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "SFC verificara todos los archivos del sistema. Esto puede tardar 10-15 minutos.\n\nContinuar?",
            "Verificar archivos del sistema", MessageBoxButton.OKCancel, MessageBoxImage.Information);

        if (result == MessageBoxResult.OK)
        {
            ShowStatus("Ejecutando SFC /scannow... Esto puede tardar varios minutos.", true);
            var (ok, msg) = await WindowsUpdateService.RunSFCAsync();
            ShowStatus(msg, ok);
        }
    }

    private async void RunDISM_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "DISM reparara la imagen de Windows. Esto puede tardar 10-20 minutos.\n\nContinuar?",
            "Reparar imagen de Windows", MessageBoxButton.OKCancel, MessageBoxImage.Information);

        if (result == MessageBoxResult.OK)
        {
            ShowStatus("Ejecutando DISM /RestoreHealth... Esto puede tardar varios minutos.", true);
            var (ok, msg) = await WindowsUpdateService.RunDISMAsync();
            ShowStatus(msg, ok);
        }
    }

    private void ShowStatus(string message, bool success)
    {
        StatusBar.Visibility = Visibility.Visible;
        StatusText.Text = message;
        StatusIcon.Text = success ? "+" : "!";
        StatusIcon.Foreground = success
            ? new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69))
            : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
    }
}
