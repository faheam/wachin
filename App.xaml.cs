using System.Windows;
using System.Windows.Threading;
using Wachin.Services;

namespace Wachin;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Global exception handlers
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                MessageBox.Show($"Error fatal:\n\n{ex}", "Wachin — Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Error inesperado:\n\n{args.Exception}", "Wachin — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            base.OnStartup(e);
            AppServices.Init();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al iniciar Wachin:\n\n{ex}", "Wachin — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}
