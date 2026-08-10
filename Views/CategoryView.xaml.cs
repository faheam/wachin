using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Wachin.Models;
using Wachin.Services;
using Wachin.ViewModels;

namespace Wachin.Views;

public partial class CategoryView : UserControl
{
    private readonly TweakCategory _category;
    private readonly List<TweakItemViewModel> _tweaks;

    public CategoryView(TweakCategory category, List<TweakItemViewModel> tweaks)
    {
        InitializeComponent();
        _category = category;
        _tweaks = tweaks;

        CatTitle.Text = new CategoryToNameConverter().Convert(category, null!, null, null)?.ToString() ?? "";
        CatDesc.Text = new CategoryToDescriptionConverter().Convert(category, null!, null, null)?.ToString() ?? "";

        TweakList.ItemsSource = tweaks;

        Loaded += (_, _) =>
        {
            UpdateProgress();
            // Subscribe to tweak changes
            foreach (var vm in _tweaks)
                vm.PropertyChanged += (_, _) => UpdateProgress();

            // Post-process each item to bind restart/applied badge visibility
            foreach (var item in TweakList.Items)
            {
                if (item is not TweakItemViewModel vm) continue;
                var container = TweakList.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                if (container == null) continue;

                var restart = container.FindName("RestartBadge") as Border;
                var explorer = container.FindName("ExplorerBadge") as Border;
                var applied = container.FindName("AppliedBadge") as Border;

                if (vm.Tweak.NeedsRestart && restart != null) restart.Visibility = Visibility.Visible;
                if (vm.Tweak.NeedsExplorerRestart && explorer != null) explorer.Visibility = Visibility.Visible;

                void UpdateStatus()
                {
                    if (applied != null) applied.Visibility = vm.IsApplied ? Visibility.Visible : Visibility.Collapsed;
                }

                UpdateStatus();
                vm.PropertyChanged += (_, _) => UpdateStatus();
            }

            // Entrada escalonada: header + tarjetas de tweaks
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                EntranceAnimations.AnimateElement(HeaderCard, 0, 12, 240);
                int idx = 0;
                foreach (var item in TweakList.Items)
                {
                    if (TweakList.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement c)
                        EntranceAnimations.AnimateElement(c, 40 + idx++ * 35, 12, 240);
                }
            }));
        };
    }

    private void UpdateProgress()
    {
        int total = _tweaks.Count;
        int applied = _tweaks.Count(t => t.IsApplied);
        int remaining = total - applied;

        if (total == 0)
        {
            ProgressText.Text = "Sin ajustes disponibles";
            UndoAllBtn.Visibility = Visibility.Collapsed;
        }
        else if (applied == 0)
        {
            ProgressText.Text = $"{total} ajustes disponibles";
            UndoAllBtn.Visibility = Visibility.Collapsed;
        }
        else if (applied == total)
        {
            ProgressText.Text = $"Todos los {total} ajustes aplicados";
            UndoAllBtn.Visibility = Visibility.Visible;
        }
        else
        {
            ProgressText.Text = $"{applied} de {total} aplicados — {remaining} restantes";
            UndoAllBtn.Visibility = Visibility.Visible;
        }
    }

    private async void OnUndoAll(object sender, RoutedEventArgs e)
    {
        var applied = _tweaks.Where(t => t.IsApplied).ToList();
        int count = 0;
        foreach (var vm in applied)
        {
            var (ok, _) = await Task.Run(() => TweakEngine.Undo(vm.Tweak, AppServices.State));
            if (ok)
            {
                vm.IsApplied = false;
                count++;
            }
        }
        if (count > 0)
            AppServices.ShowSuccess("Deshacer", $"{count} ajustes deshechos en esta categoría.");
    }
}
