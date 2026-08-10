using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.Views;

public sealed class ScriptVm
{
    public AutomationScript Script { get; }
    public string Title => Script.Title;
    public string Description => Script.Description;
    public string CategoryLabel => Script.Category;
    public string TimeLabel => AutomationService.FormatTime(Script.EstimatedTimeSeconds);
    public string AdminLabel => Script.RequiresAdmin ? "Admin" : "Usuario";
    public string RiskLabel => Script.Risk switch
    {
        RiskLevel.Low => "Riesgo bajo",
        RiskLevel.Medium => "Riesgo medio",
        RiskLevel.High => "Riesgo alto",
        _ => ""
    };
    public Brush RiskColor => Script.Risk switch
    {
        // Tonos 700-level: contraste suficiente para el texto blanco del badge
        RiskLevel.Low => new SolidColorBrush(Color.FromRgb(0x04, 0x78, 0x57)),
        RiskLevel.Medium => new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09)),
        RiskLevel.High => new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C)),
        _ => Brushes.Gray
    };
    private bool _isRunning;
    public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(ButtonText)); OnPropertyChanged(nameof(ButtonStyle)); } }
    public string ButtonText => IsRunning ? "Ejecutando..." : "Ejecutar";

    // Devuelve el Style real (no un string): bindear un string a Style no funciona en WPF
    public Style ButtonStyle => IsRunning
        ? ResourceBrush.Style("BaseButton")
        : ResourceBrush.Style("PrimaryButton");

    public ScriptVm(AutomationScript script) { Script = script; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public partial class AutomationView : UserControl
{
    private readonly List<ScriptVm> _vms = new();

    public AutomationView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var scripts = AutomationService.GetScripts();
        _vms.Clear();
        foreach (var s in scripts)
        {
            var vm = new ScriptVm(s);
            vm.PropertyChanged += (s, e) => { };
            _vms.Add(vm);
        }
        ScriptsList.ItemsSource = _vms;
    }

    private async void RunScript_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ScriptVm vm)
        {
            if (vm.IsRunning) return;

            var script = vm.Script;

            // Confirm for high-risk
            if (script.Risk >= RiskLevel.Medium)
            {
                var result = MessageBox.Show(
                    $"Vas a ejecutar: {script.Title}\n\n{script.Description}\n\nEstas seguro?",
                    "Confirmar ejecucion", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

                if (result != MessageBoxResult.OK) return;
            }

            vm.IsRunning = true;
            ShowStatus($"Ejecutando: {script.Title}...", true, 10);

            try
            {
                var scriptResult = await AutomationService.RunScriptAsync(script, progress =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = progress * 100;
                    });
                });

                if (scriptResult.Success)
                {
                    ShowStatus($"{script.Title} completado en {scriptResult.Duration.TotalSeconds:F1}s", true, 100);
                }
                else
                {
                    ShowStatus($"Error en {script.Title}: {scriptResult.Error}", false, 0);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error inesperado: {ex.Message}", false, 0);
            }
            finally
            {
                vm.IsRunning = false;
            }
        }
    }

    private void ShowStatus(string message, bool success, double progress)
    {
        StatusBar.Visibility = Visibility.Visible;
        StatusText.Text = message;
        StatusIcon.Text = success ? "+" : "!";
        StatusIcon.Foreground = success
            ? new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69))
            : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        ProgressBar.Value = progress;
    }
}
