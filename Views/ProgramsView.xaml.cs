using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.Views;

public sealed class ProgramVm : INotifyPropertyChanged
{
    public ProgramItem Program { get; }
    public string Title => Program.Name;
    public string Description => Program.Description;
    public string Category => Program.Category;
    public string SourceLabel => Program.WingetId != null ? "winget" : "Web oficial";
    public string SizeLabel => Program.SizeMb > 0 ? $"~{Program.SizeMb} MB" : "";
    public Visibility SizeVisibility => Program.SizeMb > 0 ? Visibility.Visible : Visibility.Collapsed;
    public string Homepage => Program.Homepage ?? "";

    /// <summary>Sin winget ni descarga directa: el boton abre la web oficial.</summary>
    public bool IsManualOnly => Program.WingetId == null && Program.DownloadUrl == null;

    private bool _isInstalling;
    public bool IsInstalling { get => _isInstalling; set { _isInstalling = value; OnPropertyChanged(); OnPropertyChanged(nameof(ButtonText)); OnPropertyChanged(nameof(ButtonStyle)); } }
    public string ButtonText => IsInstalling ? "Instalando..." : (IsManualOnly ? "Abrir web" : "Instalar");

    // Devuelve el Style real (no un string): bindear un string a Style no funciona en WPF
    public Style ButtonStyle => IsInstalling
        ? ResourceBrush.Style("BaseButton")
        : ResourceBrush.Style("PrimaryButton");

    public ProgramVm(ProgramItem program) { Program = program; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public partial class ProgramsView : UserControl
{
    private readonly List<ProgramVm> _vms = new();
    private bool _loaded;

    public ProgramsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return; // la vista se cachea: no duplicar items
        _loaded = true;

        foreach (var p in ProgramsService.GetPrograms())
            _vms.Add(new ProgramVm(p));

        var cvs = new CollectionViewSource { Source = _vms };
        cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ProgramVm.Category)));
        ProgramsList.ItemsSource = cvs.View;

        bool wingetOk = await ProgramsService.IsWingetAvailableAsync();
        WingetBanner.Visibility = Visibility.Visible;
        if (wingetOk)
        {
            BannerIcon.Text = "+";
            BannerIcon.Foreground = ResourceBrush.Safe("Success", new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)));
            BannerText.Text = "Winget disponible: los programas se descargan e instalan desde los repositorios oficiales de cada publisher.";
        }
        else
        {
            BannerIcon.Text = "!";
            BannerIcon.Foreground = ResourceBrush.Safe("Danger", new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)));
            BannerText.Text = "Winget no detectado en este equipo. Las apps sin descarga directa abriran su web oficial. Para habilitar winget, instalá la app 'App Installer' desde Microsoft Store.";
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProgramVm vm } || vm.IsInstalling) return;

        // Sin script automatizado: abrir la pagina oficial del programa
        if (vm.IsManualOnly)
        {
            OpenUrl(vm.Homepage);
            return;
        }

        var result = MessageBox.Show(
            $"Vas a instalar: {vm.Title}\n\n{vm.Description}\n\n¿Querés continuar?",
            "Confirmar instalacion", MessageBoxButton.OKCancel, MessageBoxImage.Information);
        if (result != MessageBoxResult.OK) return;

        vm.IsInstalling = true;
        ShowStatus($"Descargando e instalando {vm.Title}...", success: false, working: true);
        try
        {
            var (ok, msg) = await ProgramsService.InstallAsync(vm.Program);
            ShowStatus(msg, ok, working: false);
            if (ok) AppServices.ShowSuccess(vm.Title, msg);
            else AppServices.ShowError("Error", msg);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error inesperado: {ex.Message}", success: false, working: false);
            AppServices.ShowError("Error", ex.Message);
        }
        finally
        {
            vm.IsInstalling = false;
        }
    }

    private void OpenHomepage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink { Tag: ProgramVm vm })
            OpenUrl(vm.Homepage);
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void ShowStatus(string message, bool success, bool working)
    {
        StatusBar.Visibility = Visibility.Visible;
        StatusText.Text = message;
        StatusIcon.Text = working ? "*" : (success ? "+" : "!");
        StatusIcon.Foreground = success
            ? new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69))
            : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        ProgressBar.IsIndeterminate = working;
        ProgressBar.Value = working ? 0 : (success ? 100 : 0);
    }
}
