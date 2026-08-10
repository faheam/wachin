using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Wachin.Models;
using Wachin.Services;
using Wachin.ViewModels;

namespace Wachin.Views;

public partial class OverviewView : UserControl
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _hudTimer;

    public OverviewView(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        Loaded += OnLoaded;
        // La vista queda cacheada en MainViewModel: al volver a Inicio el evento
        // Loaded no se dispara de nuevo, asi que escuchamos cambios del catalogo
        // para mantener "Ajustes aplicados" siempre al dia.
        TweakEngine.CatalogChanged += OnCatalogChanged;
        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _hudTimer.Tick += (_, _) => UpdateHud();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AdminStatus.Text = AppServices.IsAdmin
            ? "NIVEL: ADMINISTRADOR"
            : "NIVEL: USUARIO";

        RefreshAppliedSummary();

        UpdateHud();
        _hudTimer.Start();

        // Nombre del procesador: consulta ligera cacheada, una sola vez
        _ = LoadCpuNameAsync();

        // Entrada escalonada de las tarjetas del dashboard
        EntranceAnimations.FadeUpStaggered(RootPanel, 45, 14, 260);
    }

    private void OnCatalogChanged()
    {
        // CatalogChanged puede dispararse desde un hilo de fondo (Task.Run)
        Dispatcher.InvokeAsync(RefreshAppliedSummary);
    }

    private void RefreshAppliedSummary()
    {
        var applied = AppServices.State.AppliedTweaks;
        if (applied.Count == 0)
        {
            AppliedSummary.Text = "Aun no aplicaste ningun ajuste. Elegi una categoria y empeza!";
            UndoAllBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            var groups = applied.GroupBy(a => a.Category).OrderBy(g => g.Key);
            var lines = groups.Select(g => $"- {GetCatName(g.Key)}: {g.Count()}");
            AppliedSummary.Text = $"{applied.Count} ajustes activos:\n" + string.Join("\n", lines);
            UndoAllBtn.Visibility = Visibility.Visible;
        }
    }

    private void UpdateHud()
    {
        try
        {
            var s = SystemInfoService.GetQuickStatus();
            HudRamPct.Text = $"{s.RamPercent}%";
            HudRamBar.Value = s.RamPercent;
            HudRamDetail.Text = $"{s.RamUsed} / {s.RamTotal}";
            HudUptime.Text = s.Uptime;
            HudDate.Text = s.Date;
            HudClock.Text = s.Clock;
        }
        catch { }
    }

    private async Task LoadCpuNameAsync()
    {
        try
        {
            string name = await Task.Run(SystemInfoService.GetCpuName);
            HudCpuName.Text = name;
        }
        catch
        {
            HudCpuName.Text = "No detectado";
        }
    }

    private void OnOpenCleaner(object sender, RoutedEventArgs e) =>
        _vm.SelectedNav = new NavItemVm { Id = "cleaner", Title = "Limpiador", Glyph = "\uE74D" };

    private void OnUndoAll(object sender, RoutedEventArgs e) => _vm.UndoAllCommand.Execute(null);

    private static string GetCatName(Models.TweakCategory cat) => cat switch
    {
        Models.TweakCategory.Performance => "Rendimiento",
        Models.TweakCategory.Privacy => "Privacidad",
        Models.TweakCategory.Gpu => "GPU",
        Models.TweakCategory.Power => "Energia",
        Models.TweakCategory.Desktop => "Escritorio",
        Models.TweakCategory.Taskbar => "Barra de tareas",
        Models.TweakCategory.Explorer => "Explorador",
        Models.TweakCategory.Gaming => "Juegos",
        Models.TweakCategory.System => "Sistema",
        _ => cat.ToString()
    };
}
