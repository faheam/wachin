using System.IO;

namespace Wachin.Services;

/// <summary>
/// Helpers para lidiar con los falsos positivos del antivirus:
/// agrega Wachin a las exclusiones de Microsoft Defender.
/// </summary>
public static class DefenderService
{
    /// <summary>Carpeta donde vive el ejecutable (EXE portable de un solo archivo).</summary>
    public static string AppFolder =>
        Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory) ?? AppContext.BaseDirectory;

    /// <summary>Nombre del proceso actual (p. ej. "Wachin.exe").</summary>
    public static string AppProcess => Path.GetFileName(Environment.ProcessPath ?? "Wachin.exe");

    /// <summary>
    /// Agrega la carpeta y el proceso de Wachin a las exclusiones de Microsoft Defender.
    /// Solo afecta a Defender; los antivirus de terceros se excluyen manualmente.
    /// </summary>
    public static async Task<(bool Ok, string Message)> AddAppExclusionAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                string ps =
                    $"Add-MpPreference -ExclusionPath '{AppFolder}' -ExclusionProcess '{AppProcess}' -ErrorAction Stop";
                var (code, _, err) = ProcessRunner.Run("powershell.exe",
                    $"-NoProfile -NonInteractive -Command \"{ps}\"", 30_000);

                if (code == 0)
                    return (true, "Wachin ahora está excluido de Microsoft Defender.");
                return (false, string.IsNullOrWhiteSpace(err)
                    ? "El comando de exclusión falló. Verificá que Microsoft Defender esté activo."
                    : err);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        });
    }

    /// <summary>
    /// Consulta si la carpeta y el proceso de Wachin ya figuran en las exclusiones de Defender.
    /// </summary>
    public static async Task<(bool Ok, bool Excluded, string Message)> GetExclusionStatusAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                string ps =
                    "$p = Get-MpPreference -ErrorAction Stop; " +
                    "[bool]($p.ExclusionPath -contains '" + AppFolder + "') -and " +
                    "[bool]($p.ExclusionProcess -contains '" + AppProcess + "')";
                var (code, stdout, err) = ProcessRunner.Run("powershell.exe",
                    $"-NoProfile -NonInteractive -Command \"{ps}\"", 20_000);

                if (code != 0)
                    return (false, false, string.IsNullOrWhiteSpace(err)
                        ? "Microsoft Defender no está disponible en este equipo."
                        : err);

                return (true, stdout.Trim().StartsWith("True"), "");
            }
            catch (Exception ex)
            {
                return (false, false, ex.Message);
            }
        });
    }

    /// <summary>Abre la app Seguridad de Windows para exclusiones manuales (antivirus de terceros).</summary>
    public static async Task<(bool Ok, string Message)> OpenWindowsSecurityAsync()
    {
        try
        {
            await Task.Run(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "windowsdefender:",
                UseShellExecute = true
            }));
            return (true, "Seguridad de Windows abierta.");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }
}
