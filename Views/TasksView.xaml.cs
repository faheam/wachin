using System.Windows;
using System.Windows.Controls;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.Views;

public partial class TasksView : UserControl
{
    private List<SchedTaskItem> _all = new();

    public TasksView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadTasksAsync();
    }

    private async Task LoadTasksAsync()
    {
        _all = await Task.Run(() => ScheduledTaskService.GetAll());
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = SearchBox?.Text?.ToLowerInvariant() ?? "";
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _all
            : _all.Where(t => t.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || t.Action.Contains(query, StringComparison.OrdinalIgnoreCase))
                  .ToList();
        TaskList.ItemsSource = filtered;
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadTasksAsync();

    private void OnSearch(object sender, TextChangedEventArgs e) => ApplyFilter();

    private async void OnToggle(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SchedTaskItem item)
        {
            var (ok, msg) = await Task.Run(() => ScheduledTaskService.SetEnabled(item.Path, !item.Enabled));
            if (ok)
            {
                item.Enabled = !item.Enabled;
                await LoadTasksAsync();
            }
            else
            {
                AppServices.ShowError("Error", msg);
            }
        }
    }
}
