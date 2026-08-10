using System.Windows;
using System.Windows.Controls;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.Views;

public partial class SystemInfoView : UserControl
{
    private SysInfo? _info;

    public SystemInfoView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadInfoAsync();
    }

    private async Task LoadInfoAsync()
    {
        LoadingBar.Visibility = Visibility.Visible;
        LoadingText.Visibility = Visibility.Visible;

        _info = await Task.Run(() => SystemInfoService.Collect());

        OsText.Text = $"{_info.OsName} {_info.OsVersion} (Build {_info.OsBuild}) — {_info.OsArch}\nEquipo: {_info.MachineName}";
        CpuText.Text = $"{_info.CpuName}\nNúcleos: {_info.CpuCores}  |  Hilos: {_info.CpuThreads}  |  Frecuencia: {_info.CpuSpeed}";
        RamText.Text = $"{DiskInfo.FormatBytes(_info.RamUsed)} / {DiskInfo.FormatBytes(_info.RamTotal)} ({_info.RamPercent}% usado)";
        RamBar.Value = _info.RamPercent;
        GpuText.Text = string.IsNullOrEmpty(_info.GpuName) ? "No detectada" : $"{_info.GpuName}\nVRAM: {_info.GpuVram}  |  Driver: {_info.GpuDriver}";

        DisksPanel.Children.Clear();
        foreach (var d in _info.Disks)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            sp.Children.Add(new TextBlock
            {
                Text = $"{d.Id} {d.Label} — libre: {d.FreeLabel} de {d.TotalLabel}",
                Style = (Style)FindResource("Body")
            });
            var bar = new ProgressBar { Height = 6, Maximum = 100, Value = d.UsagePercent, Margin = new Thickness(0, 4, 0, 0) };
            sp.Children.Add(bar);
            DisksPanel.Children.Add(sp);
        }

        if (!string.IsNullOrEmpty(_info.BatteryPercent))
        {
            BatteryCard.Visibility = Visibility.Visible;
            BatteryText.Text = $"{_info.BatteryPercent} — {_info.BatteryStatus}";
        }

        string sys = "";
        if (!string.IsNullOrEmpty(_info.Motherboard)) sys += $"Placa: {_info.Motherboard}\n";
        if (!string.IsNullOrEmpty(_info.Bios)) sys += $"BIOS: {_info.Bios}\n";
        if (!string.IsNullOrEmpty(_info.Uptime)) sys += $"Tiempo activo: {_info.Uptime}";
        SysText.Text = sys.Trim();

        LoadingBar.Visibility = Visibility.Collapsed;
        LoadingText.Visibility = Visibility.Collapsed;
        InfoPanel.Visibility = Visibility.Visible;
    }

    private void OnCopyReport(object sender, RoutedEventArgs e)
    {
        if (_info != null)
        {
            string report = SystemInfoService.FormatReport(_info);
            Clipboard.SetText(report);
            AppServices.ShowSuccess("Copiado", "El informe se copió al portapapeles.");
        }
    }
}
