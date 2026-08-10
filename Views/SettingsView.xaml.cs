using System.Windows;
using System.Windows.Controls;
using Wachin.Models;
using Wachin.Services;

namespace Wachin.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            UpdateToggles();
            await RefreshExclusionStatusAsync();
        };
    }

    private void UpdateToggles()
    {
        ConfirmToggle.IsChecked = AppServices.State.Settings.ConfirmBeforeApply;
        RestoreReminderToggle.IsChecked = AppServices.State.Settings.RemindRestorePoint;
    }

    private void OnConfirmChanged(object sender, RoutedEventArgs e)
    {
        AppServices.State.Settings.ConfirmBeforeApply = ConfirmToggle.IsChecked == true;
        AppServices.State.Save();
    }

    private void OnRestoreReminderChanged(object sender, RoutedEventArgs e)
    {
        AppServices.State.Settings.RemindRestorePoint = RestoreReminderToggle.IsChecked == true;
        AppServices.State.Save();
    }

    private async void OnExcludeFromDefender(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "¿Agregar a Wachin a las exclusiones de Microsoft Defender?\n\n" +
            "Se excluirá esta carpeta:\n" + DefenderService.AppFolder + "\n\n" +
            "Esto evita que el antivirus lo marque como falso positivo. Solo aplica a Microsoft " +
            "Defender: si usás otro antivirus, tenés que agregar la exclusión manualmente.",
            "Excluir del antivirus",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        ExcludeBtn.IsEnabled = false;
        try
        {
            var (ok, msg) = await DefenderService.AddAppExclusionAsync();
            if (ok)
                AppServices.ShowSuccess("Exclusión agregada", msg);
            else
                AppServices.ShowError("No se pudo excluir", msg);
        }
        finally
        {
            ExcludeBtn.IsEnabled = true;
            await RefreshExclusionStatusAsync();
        }
    }

    private async void OnOpenWindowsSecurity(object sender, RoutedEventArgs e)
    {
        var (ok, msg) = await DefenderService.OpenWindowsSecurityAsync();
        if (ok)
            AppServices.ShowSuccess("Listo", msg);
        else
            AppServices.ShowError("No se pudo abrir", msg);
    }

    private async Task RefreshExclusionStatusAsync()
    {
        var (ok, excluded, _) = await DefenderService.GetExclusionStatusAsync();
        if (!ok)
        {
            ExclusionStatus.Text =
                "No se pudo consultar Microsoft Defender (¿antivirus de terceros o Defender desactivado?).";
            return;
        }

        ExclusionStatus.Text = excluded
            ? "✓ Wachin ya está excluido de Microsoft Defender."
            : "Wachin todavía no está excluido. Usá el botón de arriba.";
    }

    private void OnResetState(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "¿Estás seguro? Se borrará todo el estado de Wachin (ajustes aplicados, inicio deshabilitado, etc.).",
            "Restablecer estado",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            AppServices.State.ClearAll();
            // Refresca el resumen de Inicio y los contadores de la barra lateral
            TweakEngine.NotifyCatalogChanged();
            AppServices.ShowSuccess("Listo", "Estado restablecido. Wachin vuelve a su configuración inicial.");
        }
    }
}
