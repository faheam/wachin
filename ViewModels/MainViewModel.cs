using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<NavItemVm> NavItems { get; } = new();

    private object? _currentView;
    public object? CurrentView { get => _currentView; set { _currentView = value; OnPropertyChanged(); } }

    private string _pageTitle = "Inicio";
    public string PageTitle { get => _pageTitle; set { _pageTitle = value; OnPropertyChanged(); } }

    private string _pageSubtitle = "";
    public string PageSubtitle { get => _pageSubtitle; set { _pageSubtitle = value; OnPropertyChanged(); } }

    private NavItemVm? _selectedNav;
    public NavItemVm? SelectedNav { get => _selectedNav; set { _selectedNav = value; OnPropertyChanged(); SelectNav(); } }

    public AsyncRelayCommand CreateRestorePointCommand { get; }
    public RelayCommand RestartExplorerCommand { get; }
    public RelayCommand UndoAllCommand { get; }

    private readonly Dictionary<string, object> _views = new();

    // Category → Tweak list cache
    public List<TweakItemViewModel> AllTweaks { get; } = new();

    public MainViewModel()
    {
        CreateRestorePointCommand = new AsyncRelayCommand(CreateRestorePointAsync);
        RestartExplorerCommand = new RelayCommand(RestartExplorer);
        UndoAllCommand = new RelayCommand(_ => UndoAll());

        BuildNav();
        BuildTweaks();
        SubscribeChanges();
    }

    private void BuildNav()
    {
        NavItems.Add(new NavItemVm { Id = "home", Title = "Inicio", Glyph = "\uE80F" });
        NavItems.Add(new NavItemVm { Id = "cat-head", Title = "CATEGORÍAS", IsHeader = true });
        NavItems.Add(new NavItemVm { Id = "perf", Title = "Rendimiento", Glyph = "\uE945" });
        NavItems.Add(new NavItemVm { Id = "priv", Title = "Privacidad", Glyph = "\uE72E" });
        NavItems.Add(new NavItemVm { Id = "gpu", Title = "GPU y gráficos", Glyph = "\uE714" });
        NavItems.Add(new NavItemVm { Id = "power", Title = "Energía", Glyph = "\uE850" });
        NavItems.Add(new NavItemVm { Id = "desk", Title = "Escritorio", Glyph = "\uE7F4" });
        NavItems.Add(new NavItemVm { Id = "taskbar", Title = "Barra de tareas", Glyph = "\uE90E" });
        NavItems.Add(new NavItemVm { Id = "explorer", Title = "Explorador", Glyph = "\uE8B7" });
        NavItems.Add(new NavItemVm { Id = "gaming", Title = "Juegos", Glyph = "\uE7FC" });
        NavItems.Add(new NavItemVm { Id = "sys", Title = "Sistema", Glyph = "\uE713" });
        NavItems.Add(new NavItemVm { Id = "tool-head", Title = "HERRAMIENTAS", IsHeader = true });
        NavItems.Add(new NavItemVm { Id = "sysinfo", Title = "Info del sistema", Glyph = "\uE70F" });
        NavItems.Add(new NavItemVm { Id = "startup", Title = "Inicio", Glyph = "\uE7C4" });
        NavItems.Add(new NavItemVm { Id = "tasks", Title = "Tareas", Glyph = "\uE823" });
        NavItems.Add(new NavItemVm { Id = "cleaner", Title = "Limpiador", Glyph = "\uE74D" });
        NavItems.Add(new NavItemVm { Id = "bloat", Title = "Bloatware", Glyph = "\uE71D" });
        NavItems.Add(new NavItemVm { Id = "services", Title = "Servicios", Glyph = "\uE777" });
        NavItems.Add(new NavItemVm { Id = "updates", Title = "Windows Update", Glyph = "\uE777" });
        NavItems.Add(new NavItemVm { Id = "automation", Title = "Automatización", Glyph = "\uE9D5" });
        NavItems.Add(new NavItemVm { Id = "notebook", Title = "Notebook", Glyph = "\uE850" });
        NavItems.Add(new NavItemVm { Id = "settings-head", Title = "", IsHeader = true });
        NavItems.Add(new NavItemVm { Id = "settings", Title = "Configuración", Glyph = "\uE713" });
    }

    private void BuildTweaks()
    {
        foreach (var t in TweakCatalog.All)
            AllTweaks.Add(new TweakItemViewModel(t, this));
    }

    private void SubscribeChanges()
    {
        TweakEngine.CatalogChanged += RefreshBadges;
        RefreshBadges();
    }

    public void RefreshBadges()
    {
        foreach (var nav in NavItems.Where(n => !n.IsHeader))
        {
            TweakCategory? cat = nav.Id switch
            {
                "perf" => TweakCategory.Performance, "priv" => TweakCategory.Privacy,
                "gpu" => TweakCategory.Gpu, "power" => TweakCategory.Power,
                "desk" => TweakCategory.Desktop, "taskbar" => TweakCategory.Taskbar,
                "explorer" => TweakCategory.Explorer, "gaming" => TweakCategory.Gaming,
                "sys" => TweakCategory.System, _ => null
            };
            if (cat.HasValue)
            {
                int total = AllTweaks.Count(t => t.Tweak.Category == cat.Value);
                int applied = AppServices.State.CountApplied(cat.Value);
                nav.TotalTweaks = total;
                nav.AppliedCount = applied;
            }
        }
    }

    public List<TweakItemViewModel> GetTweaksForCategory(TweakCategory cat)
        => AllTweaks.Where(t => t.Tweak.Category == cat).ToList();

    // ── Navigation ─────────────────────────────────────────────────────────

    private void SelectNav()
    {
        if (SelectedNav == null) return;

        object view;
        if (!_views.TryGetValue(SelectedNav.Id, out var cached))
        {
            view = CreateView(SelectedNav.Id);
            _views[SelectedNav.Id] = view;
        }
        else
        {
            view = cached;
        }

        CurrentView = view;

        // Update page header
        var (title, subtitle) = SelectedNav.Id switch
        {
            "home" => ("Inicio", "CREA PUNTO DE RESTAURACIÓN NO SEAS PELOTUDO."),
            "perf" => ("Rendimiento", "Hace que tu PC responda más rápido"),
            "priv" => ("Privacidad", "Controla qué datos recopila Windows"),
            "gpu" => ("GPU", "Ajustes de la tarjeta gráfica"),
            "power" => ("Energía", "Planes, hibernación y suspensión USB"),
            "desk" => ("Escritorio", "Iconos y apariencia"),
            "taskbar" => ("Barra de tareas", "Botones, reloj y widgets"),
            "explorer" => ("Explorador", "Cómo se ven las ventanas de archivos"),
            "gaming" => ("Juegos", "Modo juego, Game Bar y latencia"),
            "sys" => ("Sistema", "Apagado y opciones generales"),
            "sysinfo" => ("Info del sistema", "Datos de tu computadora"),
            "startup" => ("Programas de inicio", "Qué se ejecuta al encender el PC"),
            "tasks" => ("Tareas programadas", "Tareas automáticas de Windows y apps"),
            "cleaner" => ("Limpiador", "Elimina archivos temporales"),
            "bloat" => ("Bloatware", "Apps preinstaladas que puedes quitar"),
            "services" => ("Servicios", "Servicios de Windows activos"),
            "updates" => ("Windows Update", "Actualizaciones y reparación del sistema"),
            "automation" => ("Automatización", "Scripts de mantenimiento y limpieza"),
            "notebook" => ("Notebook", "Control de temperatura del procesador"),
            "settings" => ("Configuración", "Ajustes y sobre Wachin"),
            _ => (SelectedNav.Title, "")
        };
        PageTitle = title;
        PageSubtitle = subtitle;
    }

    private object CreateView(string id)
    {
        return id switch
        {
            "home" => new Views.OverviewView(this),
            "perf" => CreateCategoryView(TweakCategory.Performance),
            "priv" => CreateCategoryView(TweakCategory.Privacy),
            "gpu" => CreateCategoryView(TweakCategory.Gpu),
            "power" => CreateCategoryView(TweakCategory.Power),
            "desk" => CreateCategoryView(TweakCategory.Desktop),
            "taskbar" => CreateCategoryView(TweakCategory.Taskbar),
            "explorer" => CreateCategoryView(TweakCategory.Explorer),
            "gaming" => CreateCategoryView(TweakCategory.Gaming),
            "sys" => CreateCategoryView(TweakCategory.System),
            "sysinfo" => new Views.SystemInfoView(),
            "startup" => new Views.StartupView(),
            "tasks" => new Views.TasksView(),
            "cleaner" => new Views.CleanerView(),
            "bloat" => new Views.BloatwareView(),
            "services" => new Views.ServicesView(),
            "updates" => new Views.WindowsUpdateView(),
            "automation" => new Views.AutomationView(),
            "notebook" => new Views.NotebookView(),
            "settings" => new Views.SettingsView(),
            _ => new Views.OverviewView(this)
        };
    }

    private Views.CategoryView CreateCategoryView(TweakCategory cat)
    {
        var tweaks = GetTweaksForCategory(cat);
        return new Views.CategoryView(cat, tweaks);
    }

    // ── Actions ────────────────────────────────────────────────────────────

    private async Task CreateRestorePointAsync()
    {
        if (!RestorePointService.IsProtectionEnabled())
        {
            AppServices.ShowWarning("Protección desactivada",
                "La restauración del sistema está desactivada. Actívala desde Configuración del sistema → Protección.");
            try { RestorePointService.OpenSystemProtectionSettings(); } catch { }
            return;
        }

        await Task.Run(() =>
        {
            var (ok, msg) = RestorePointService.Create("Wachin — punto antes de ajustes");
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ok) AppServices.ShowSuccess("Listo", msg);
                else AppServices.ShowError("Error", msg);
            });
        });
    }

    private void RestartExplorer()
    {
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("explorer"))
                p.Kill();
            System.Diagnostics.Process.Start("explorer.exe");
            AppServices.ShowSuccess("Explorador reiniciado", "Los cambios de la barra de tareas y el explorador deberían aplicarse ahora.");
        }
        catch (Exception ex)
        {
            AppServices.ShowError("Error", $"No se pudo reiniciar el Explorador: {ex.Message}");
        }
    }

    private void UndoAll()
    {
        var applied = AppServices.State.AppliedTweaks.ToList();
        int count = 0;
        foreach (var rec in applied)
        {
            var tweak = TweakCatalog.All.FirstOrDefault(t => t.Id == rec.Id);
            if (tweak != null)
            {
                var (ok, _) = TweakEngine.Undo(tweak, AppServices.State);
                if (ok) count++;
            }
        }
        if (count > 0)
            AppServices.ShowSuccess("Deshacer todo", $"{count} ajustes deshechos.");
        else
            AppServices.ShowToast("Nada que deshacer", "No hay ajustes aplicados actualmente.", ToastKind.Info);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ── TweakItemViewModel ──────────────────────────────────────────────────────

public sealed class TweakItemViewModel : INotifyPropertyChanged
{
    public Tweak Tweak { get; }
    private readonly MainViewModel _parent;

    private bool _isApplied;
    public bool IsApplied { get => _isApplied; set { _isApplied = value; OnPropertyChanged(); OnPropertyChanged(nameof(ButtonText)); OnPropertyChanged(nameof(ButtonStyle)); } }

    private bool _busy;
    public bool Busy { get => _busy; set { _busy = value; OnPropertyChanged(); } }

    public string ButtonText => IsApplied ? "Deshacer" : "Aplicar";

    // Devuelve el Style real (no un string): bindear un string a Style no funciona en WPF
    public Style ButtonStyle => IsApplied
        ? ResourceBrush.Style("SmallDangerButton")
        : ResourceBrush.Style("SmallPrimaryButton");
    public string RiskLabel => Tweak.Risk switch { RiskLevel.Low => "Riesgo bajo", RiskLevel.Medium => "Riesgo medio", RiskLevel.High => "Riesgo alto", _ => "" };

    // Un solo boton que alterna: Aplicar -> Deshacer
    public AsyncRelayCommand ToggleCommand { get; }

    public TweakItemViewModel(Tweak tweak, MainViewModel parent)
    {
        Tweak = tweak;
        _parent = parent;
        _isApplied = AppServices.State.IsApplied(tweak.Id);

        ToggleCommand = new AsyncRelayCommand(async () =>
        {
            if (IsApplied) await UndoAsync();
            else await ApplyAsync();
        }, () => !Busy);
    }

    private async Task ApplyAsync()
    {
        // Confirm for medium/high risk
        if (Tweak.Risk >= RiskLevel.Medium && AppServices.State.Settings.ConfirmBeforeApply)
        {
            var result = await Controls.ConfirmDialog.ShowAsync(
                $"Aplicar: {Tweak.Title}",
                Tweak.Description,
                Tweak.Risk,
                AppServices.State.Settings.RemindRestorePoint);

            if (result == ConfirmResult.Cancel) return;

            if (result == ConfirmResult.RestoreAndApply)
            {
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    var mainVm = Application.Current.MainWindow?.DataContext as MainViewModel;
                    if (mainVm != null)
                        await mainVm.CreateRestorePointCommand.ExecuteAsync(null);
                });
                await Task.Delay(1000); // brief pause
            }
        }

        Busy = true;
        try
        {
            await Task.Run(() =>
            {
                var (ok, msg) = TweakEngine.Apply(Tweak, AppServices.State);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ok)
                    {
                        IsApplied = true;
                        string toastMsg = msg;
                        if (Tweak.NeedsRestart)
                            toastMsg += " Se recomienda reiniciar el equipo.";
                        else if (Tweak.NeedsExplorerRestart)
                            toastMsg += " Reiniciá el Explorador para ver los cambios.";

                        AppServices.ShowSuccess(Tweak.Title, toastMsg);
                    }
                    else
                    {
                        AppServices.ShowError("Error", msg);
                    }
                });
            });
        }
        finally { Busy = false; }
    }

    private async Task UndoAsync()
    {
        Busy = true;
        try
        {
            await Task.Run(() =>
            {
                var (ok, msg) = TweakEngine.Undo(Tweak, AppServices.State);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ok)
                    {
                        IsApplied = false;
                        AppServices.ShowSuccess("Deshacer", $"{Tweak.Title}: {msg}");
                    }
                    else
                    {
                        AppServices.ShowError("Error", msg);
                    }
                });
            });
        }
        finally { Busy = false; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
