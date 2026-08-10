using Wachin.Models;

namespace Wachin.Services;

public sealed class AutomationScript
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Command { get; set; } = "";
    public string Arguments { get; set; } = "";
    public bool RequiresAdmin { get; set; }
    public long EstimatedTimeSeconds { get; set; }
    public RiskLevel Risk { get; set; } = RiskLevel.Low;
}

public sealed class ScriptResult
{
    public string ScriptId { get; set; } = "";
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public DateTime RunAt { get; set; } = DateTime.Now;
}

public static class AutomationService
{
    public static List<AutomationScript> GetScripts()
    {
        return new List<AutomationScript>
        {
            // ═══════════════════════ MANTENIMIENTO ═════════════════════════════
            new()
            {
                Id = "auto-sfc",
                Title = "Verificar archivos del sistema (SFC)",
                Description = "Escanea todos los archivos protegidos del sistema y reemplaza los corruptos con copias correctas. Tarda 10-15 minutos.",
                Category = "Mantenimiento",
                Command = "sfc.exe",
                Arguments = "/scannow",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 900,
                Risk = RiskLevel.Low
            },
            new()
            {
                Id = "auto-dism",
                Title = "Reparar imagen de Windows (DISM)",
                Description = "Repara la imagen de Windows usando archivos de la nube. Soluciona errores que SFC no puede reparar. Tarda 10-20 minutos.",
                Category = "Mantenimiento",
                Command = "DISM.exe",
                Arguments = "/Online /Cleanup-Image /RestoreHealth",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 1200,
                Risk = RiskLevel.Low
            },
            new()
            {
                Id = "auto-chkdsk",
                Title = "Verificar integridad del disco (CHKDSK)",
                Description = "Escanea errores en el disco duro y intenta repararlos. Puede tardar varios minutos dependiendo del tamaño del disco.",
                Category = "Mantenimiento",
                Command = "chkdsk.exe",
                Arguments = "/f /r",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 1800,
                Risk = RiskLevel.Medium
            },
            new()
            {
                Id = "auto-defrag",
                Title = "Desfragmentar discos mecánicos",
                Description = "Optimiza el posicionamiento de archivos en discos duros mecánicos (HDD). No ejecutar en SSD.",
                Category = "Mantenimiento",
                Command = "defrag.exe",
                Arguments = "C: /O",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 600,
                Risk = RiskLevel.Medium
            },

            // ═══════════════════════ RED ═══════════════════════════════════════
            new()
            {
                Id = "auto-flushdns",
                Title = "Limpiar caché DNS",
                Description = "Borra la caché DNS local. Soluciona problemas cuando no puedes acceder a páginas web que antes funcionaban.",
                Category = "Red",
                Command = "ipconfig.exe",
                Arguments = "/flushdns",
                RequiresAdmin = false,
                EstimatedTimeSeconds = 5,
                Risk = RiskLevel.Low
            },
            new()
            {
                Id = "auto-netreset",
                Title = "Restablecer configuración de red",
                Description = "Reinicia la pila TCP/IP, la caché ARP y Winsock. Soluciona problemas de conectividad persistentes. Requiere reiniciar después.",
                Category = "Red",
                Command = "netsh.exe",
                Arguments = "int ip reset",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 30,
                Risk = RiskLevel.Medium
            },
            new()
            {
                Id = "auto-winsock",
                Title = "Restablecer Winsock",
                Description = "Reinicia el catálogo Winsock. Soluciona problemas de red causados por software malicioso o controladores corruptos.",
                Category = "Red",
                Command = "netsh.exe",
                Arguments = "winsock reset",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 30,
                Risk = RiskLevel.Medium
            },
            new()
            {
                Id = "auto-netshare",
                Title = "Restablecer compartición de archivos",
                Description = "Restablece la configuración de compartición de archivos de red. Soluciona errores al acceder a carpetas compartidas.",
                Category = "Red",
                Command = "powershell.exe",
                Arguments = "-NoProfile -Command \"Get-SmbShare | Where-Object {$_.Name -ne 'IPC$' -and $_.Name -ne 'C$' -and $_.Name -ne 'ADMIN$'} | Remove-SmbShare -Force -ErrorAction SilentlyContinue\"",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 30,
                Risk = RiskLevel.High
            },

            // ═══════════════════════ LIMPIEZA ═════════════════════════════════
            new()
            {
                Id = "auto-temp",
                Title = "Limpiar archivos temporales",
                Description = "Borra archivos temporales del sistema y del usuario que ocupan espacio innecesariamente.",
                Category = "Limpieza",
                Command = "powershell.exe",
                Arguments = "-NoProfile -Command \"Remove-Item -Path $env:TEMP\\* -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -Path 'C:\\Windows\\Temp\\*' -Recurse -Force -ErrorAction SilentlyContinue\"",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 60,
                Risk = RiskLevel.Low
            },
            new()
            {
                Id = "auto-prefetch",
                Title = "Limpiar prefetch",
                Description = "Borra la carpeta prefetch que contiene datos de las aplicaciones más usadas. Windows la volverá a crear.",
                Category = "Limpieza",
                Command = "powershell.exe",
                Arguments = "-NoProfile -Command \"Remove-Item -Path 'C:\\Windows\\Prefetch\\*' -Force -ErrorAction SilentlyContinue\"",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 30,
                Risk = RiskLevel.Low
            },
            new()
            {
                Id = "auto-fontcache",
                Title = "Reconstruir caché de fuentes",
                Description = "Fuerza la reconstrucción de la caché de fuentes del sistema. Soluciona problemas con fuentes que no se muestran correctamente.",
                Category = "Limpieza",
                Command = "powershell.exe",
                Arguments = "-NoProfile -Command \"Stop-Service FontCache; Remove-Item 'C:\\Windows\\ServiceProfiles\\LocalService\\AppData\\Local\\FontCache\\*' -Force -ErrorAction SilentlyContinue; Start-Service FontCache\"",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 30,
                Risk = RiskLevel.Low
            },

            // ═══════════════════════ PRIVACIDAD ═══════════════════════════════
            new()
            {
                Id = "auto-privfix",
                Title = "Aplicar privacidad completa",
                Description = "Aplica todos los ajustes de privacidad de Wachin de una sola vez. Desactiva telemetría, publicidad, ubicación y más.",
                Category = "Privacidad",
                Command = "powershell.exe",
                Arguments = "-NoProfile -Command \"Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name 'AllowTelemetry' -Value 0; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name 'Enabled' -Value 0\"",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 10,
                Risk = RiskLevel.Medium
            },
            new()
            {
                Id = "auto-cleartrack",
                Title = "Limpiar rastros de actividad",
                Description = "Borra historial reciente, portapapeles y datos de actividad del usuario.",
                Category = "Privacidad",
                Command = "powershell.exe",
                Arguments = "-NoProfile -Command \"Remove-Item -Path \"$env:APPDATA\\Microsoft\\Windows\\Recent\\*\" -Force -ErrorAction SilentlyContinue; Clear-Clipboard -ErrorAction SilentlyContinue\"",
                RequiresAdmin = false,
                EstimatedTimeSeconds = 10,
                Risk = RiskLevel.Low
            },

            // ═══════════════════════ SISTEMA ═══════════════════════════════════
            new()
            {
                Id = "auto-hosts",
                Title = "Restablecer archivo hosts",
                Description = "Restaura el archivo hosts del sistema a su valor por defecto. Soluciona problemas de bloqueo de sitios web.",
                Category = "Sistema",
                Command = "powershell.exe",
                Arguments = "-NoProfile -Command \"@'\\n# Copyright (c) 1993-2009 Microsoft Corp.\\n#\\n# This is a sample HOSTS file used by Microsoft TCP/IP for Windows.\\n#\\n127.0.0.1       localhost\\n::1             localhost\\n' | Out-File -FilePath 'C:\\Windows\\System32\\drivers\\etc\\hosts' -Encoding ascii -Force\"",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 10,
                Risk = RiskLevel.Medium
            },
            new()
            {
                Id = "auto-mbr",
                Title = "Reparar registro de arranque (MBR)",
                Description = "Reconstruye el registro de arranque maestro. Soluciona errores de arranque \"BOOTMGR is missing\".",
                Category = "Sistema",
                Command = "bootrec.exe",
                Arguments = "/fixmbr",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 30,
                Risk = RiskLevel.High
            },
            new()
            {
                Id = "auto-bcd",
                Title = "Reparar datos de configuración de arranque (BCD)",
                Description = "Reconstruye el almacén BCD. Soluciona errores de arranque en sistemas UEFI.",
                Category = "Sistema",
                Command = "bcdedit.exe",
                Arguments = "/rebuildbcd",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 30,
                Risk = RiskLevel.High
            },
            new()
            {
                Id = "auto-refreshenv",
                Title = "Refrescar variables de entorno",
                Description = "Recarga las variables de entorno del sistema. Soluciona problemas cuando instalas apps y no se reconocen comandos.",
                Category = "Sistema",
                Command = "powershell.exe",
                Arguments = "-NoProfile -Command \"[System.Environment]::SetEnvironmentVariable('Path', [System.Environment]::GetEnvironmentVariable('Path', 'Machine'), 'Process')\"",
                RequiresAdmin = false,
                EstimatedTimeSeconds = 5,
                Risk = RiskLevel.Low
            },

            // ═══════════════════════ ENERGÍA ═══════════════════════════════════
            new()
            {
                Id = "auto-powercfg",
                Title = "Restaurar configuración de energía",
                Description = "Restaura el plan de energía predeterminado de Windows. Soluciona problemas de rendimiento por planes personalizados.",
                Category = "Energía",
                Command = "powercfg.exe",
                Arguments = "-restoredefaultschemes",
                RequiresAdmin = true,
                EstimatedTimeSeconds = 10,
                Risk = RiskLevel.Medium
            },
            new()
            {
                Id = "auto-battery",
                Title = "Calibrar batería (laptops)",
                Description = "Genera un informe detallado del estado de la batería. No calibra, solo reporta.",
                Category = "Energía",
                Command = "powercfg.exe",
                Arguments = "/batteryreport /output C:\\battery-report.html",
                RequiresAdmin = false,
                EstimatedTimeSeconds = 10,
                Risk = RiskLevel.Low
            },
        };
    }

    public static async Task<ScriptResult> RunScriptAsync(AutomationScript script, Action<double>? onProgress = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new ScriptResult { ScriptId = script.Id };

        try
        {
            onProgress?.Invoke(0.1);

            var (code, stdout, stderr) = await ProcessRunner.RunAsync(
                script.Command, script.Arguments);

            onProgress?.Invoke(1.0);
            sw.Stop();

            result.Success = code == 0;
            result.Output = stdout;
            result.Error = stderr;
            result.Duration = sw.Elapsed;
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.Success = false;
            result.Error = ex.Message;
            result.Duration = sw.Elapsed;
        }

        return result;
    }

    public static string FormatTime(long seconds) => seconds switch
    {
        < 60 => $"{seconds} segundos",
        < 3600 => $"{seconds / 60} minutos",
        _ => $"{seconds / 3600} horas"
    };
}
