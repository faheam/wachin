using System.Windows;
using System.Windows.Media;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.Controls;

public partial class ConfirmDialog : Window
{
    public ConfirmResult Result { get; private set; } = ConfirmResult.Cancel;

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public void Configure(string title, string message, RiskLevel risk, bool showReminder)
    {
        TitleBlock.Text = title;
        DescBlock.Text = message;
        ReminderBorder.Visibility = showReminder ? Visibility.Visible : Visibility.Collapsed;

        string riskLabel = risk switch
        {
            RiskLevel.Low => "Riesgo bajo",
            RiskLevel.Medium => "Riesgo medio",
            RiskLevel.High => "Riesgo alto",
            _ => ""
        };
        RiskText.Text = riskLabel;

        RiskBadge.Background = ResourceBrush.Safe(
            risk == RiskLevel.High ? "RiskHighSoft" : "RiskMediumSoft", Brushes.Transparent);
        RiskText.Foreground = ResourceBrush.Safe(
            risk == RiskLevel.High ? "RiskHigh" : "RiskMedium", Brushes.Gray);

        ApplyBtn.Content = showReminder ? "Aplicar" : "Sí, aplicar";
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Result = ConfirmResult.Cancel;
        DialogResult = false;
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        Result = ConfirmResult.Apply;
        DialogResult = true;
    }

    public static Task<ConfirmResult> ShowAsync(string title, string message, RiskLevel risk, bool showReminder)
    {
        var dialog = new ConfirmDialog
        {
            Owner = Application.Current.MainWindow
        };
        dialog.Configure(title, message, risk, showReminder);
        bool? result = dialog.ShowDialog();
        return Task.FromResult(result == true ? ConfirmResult.Apply : ConfirmResult.Cancel);
    }
}
