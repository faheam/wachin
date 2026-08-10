using Wachin.Models;

namespace Wachin.Services;

public sealed class WUpdateItem
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string KB { get; set; } = "";
    public string Size { get; set; } = "";
    public string Category { get; set; } = "";
    public string Severity { get; set; } = "";
    public DateTime Date { get; set; }
    public bool IsInstalled { get; set; }
    public bool IsDownloading { get; set; }
    public bool IsPending { get; set; }
    public double Progress { get; set; }
}

public static class WindowsUpdateService
{
    public static async Task<List<WUpdateItem>> SearchUpdatesAsync()
    {
        return await Task.Run(() =>
        {
            var results = new List<WUpdateItem>();
            try
            {
                    // Use PowerShell to get installed updates
                var (code, stdout, _) = ProcessRunner.Run("powershell.exe",
                    "-NoProfile -Command \"Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 20 | ForEach-Object { '{0}|{1}|{2}|{3}' -f $_.HotFixID, $_.Description, $_.InstalledOn, $_.InstalledBy }\"");

                if (code == 0 && !string.IsNullOrWhiteSpace(stdout))
                {
                    foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = line.Trim().Split('|');
                        if (parts.Length >= 3)
                        {
                            results.Add(new WUpdateItem
                            {
                                KB = parts[0].Trim(),
                                Description = parts[1].Trim(),
                                Title = parts[1].Trim(),
                                Date = DateTime.TryParse(parts[2].Trim(), out var d) ? d : DateTime.MinValue,
                                Size = "N/A",
                                Severity = "Normal",
                                IsInstalled = true
                            });
                        }
                    }
                }
            }
            catch { }
            return results;
        });
    }

    public static async Task<List<string>> CheckPendingUpdatesAsync()
    {
        return await Task.Run(() =>
        {
            var pending = new List<string>();
            try
            {
                var (code, stdout, _) = ProcessRunner.Run("powershell.exe",
                    "-NoProfile -Command \"$u = (New-Object -ComObject Microsoft.Update.Session).CreateUpdateSearcher().Search('IsInstalled=0'); $u.Updates | ForEach-Object { '{0}|{1}' -f $_.Title, $_.Categories[0].Name }\"");

                if (code == 0 && !string.IsNullOrWhiteSpace(stdout))
                {
                    foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        pending.Add(line.Trim());
                    }
                }
            }
            catch { }
            return pending;
        });
    }

    public static async Task<(bool Ok, string Message)> InstallUpdateAsync(string kb)
    {
        return await Task.Run(() =>
        {
            try
            {
                var (code, _, err) = ProcessRunner.Run("powershell.exe",
                    $"-NoProfile -Command \"Get-HotFix -Id '{kb}' | Install-HotFix\"");

                if (code == 0)
                    return (true, $"Actualización {kb} instalada correctamente.");
                else
                    return (false, $"Error al instalar {kb}: {err}");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public static async Task<(bool Ok, string Message)> CheckNowAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var (code, _, err) = ProcessRunner.Run("powershell.exe",
                    "-NoProfile -Command \"(New-Object -ComObject Microsoft.Update.Session).CreateUpdateSearcher().Search('IsInstalled=0')\"");

                if (code == 0)
                    return (true, "Búsqueda de actualizaciones completada.");
                else
                    return (false, $"Error: {err}");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public static async Task<(bool Ok, string Message)> SetAutoUpdateAsync(bool enabled)
    {
        return await Task.Run(() =>
        {
            try
            {
                string val = enabled ? "4" : "2"; // 4=auto, 2=notify
                var (code, _, err) = ProcessRunner.Run("reg.exe",
                    $"add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU\" /v NoAutoUpdate /t REG_DWORD /d {(enabled ? 0 : 1)} /f");

                if (code == 0)
                    return (enabled ? (true, "Actualización automática activada.") : (true, "Actualización automática desactivada."));
                else
                    return (false, $"Error: {err}");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public static async Task<(bool Ok, string Message)> OpenUpdateSettingsAsync()
    {
        try
        {
            await Task.Run(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:windowsupdate",
                UseShellExecute = true
            }));
            return (true, "Configuración de Windows Update abierta.");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }

    public static async Task<(bool Ok, string Message)> RunDISMAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var (code, stdout, err) = ProcessRunner.Run("DISM.exe",
                    "/Online /Cleanup-Image /RestoreHealth");

                if (code == 0)
                    return (true, "DISM completado. Se verificó la integridad del sistema.");
                else
                    return (false, $"DISM error: {err}");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public static async Task<(bool Ok, string Message)> RunSFCAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var (code, stdout, err) = ProcessRunner.Run("sfc.exe", "/scannow");

                if (code == 0)
                    return (true, "SFC completado. Se verificaron archivos del sistema.");
                else
                    return (false, $"SFC error: {err}");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }
}
