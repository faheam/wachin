using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Wachin.Models;

namespace Wachin.Services;

// ─── Tweak engine ──────────────────────────────────────────────────────────

public static class TweakEngine
{
    public static event Action? CatalogChanged;

    /// <summary>Notifica a las vistas que el catalogo de ajustes aplicados cambio (p. ej. tras un reset).</summary>
    public static void NotifyCatalogChanged() => CatalogChanged?.Invoke();

    public static (bool Ok, string Message) Apply(Tweak t, AppState state)
    {
        try
        {
            if (t.CustomApply != null)
            {
                var ctx = new TweakContext { State = state, TweakId = t.Id };
                var (ok, msg) = t.CustomApply(ctx);
                if (ok)
                {
                    state.AppliedTweaks.Add(new AppliedTweak
                    {
                        Id = t.Id, Title = t.Title, Category = t.Category,
                        AppliedAt = DateTime.Now, CustomData = ctx.CustomData
                    });
                    state.Save();
                    CatalogChanged?.Invoke();
                }
                return (ok, msg);
            }

            // Registry path: capture originals then write
            var originals = new List<RegistryChange>();
            foreach (var c in t.Changes)
            {
                var (origVal, origKind, origExists) = RegistryOps.Read(c.Hive, c.Path, c.Name);
                originals.Add(new RegistryChange
                {
                    Hive = c.Hive, Path = c.Path, Name = c.Name,
                    OriginalExists = origExists, OriginalValue = origVal, OriginalKind = origKind
                });

                if (c.DeleteKey)
                {
                    if (c.KeyDefaultValue != null)
                    {
                        RegistryOps.WriteString(c.Hive, c.Path, "", c.KeyDefaultValue);
                    }
                    else
                    {
                        RegistryOps.DeleteKey(c.Hive, c.Path);
                    }
                }
                else if (c.DeleteValue)
                {
                    RegistryOps.DeleteValue(c.Hive, c.Path, c.Name);
                }
                else
                {
                    RegistryOps.Write(c.Hive, c.Path, c.Name, c.NewValue, c.Kind);
                }
            }

            state.AppliedTweaks.Add(new AppliedTweak
            {
                Id = t.Id, Title = t.Title, Category = t.Category,
                AppliedAt = DateTime.Now, Originals = originals
            });
            state.Save();
            CatalogChanged?.Invoke();
            return (true, "Ajuste aplicado correctamente.");
        }
        catch (Exception ex)
        {
            return (false, $"Error al aplicar: {ex.Message}");
        }
    }

    public static (bool Ok, string Message) Undo(Tweak t, AppState state)
    {
        var rec = state.GetApplied(t.Id);
        if (rec == null) return (false, "Este ajuste no está aplicado actualmente.");

        try
        {
            if (t.CustomUndo != null && rec.CustomData != null)
            {
                var ctx = new TweakContext { State = state, TweakId = t.Id, CustomData = rec.CustomData };
                var (ok, msg) = t.CustomUndo(ctx);
                if (ok)
                {
                    state.AppliedTweaks.Remove(rec);
                    state.Save();
                    CatalogChanged?.Invoke();
                }
                return (ok, msg);
            }

            // Restore originals
            foreach (var c in rec.Originals ?? new())
            {
                if (c.OriginalExists)
                {
                    RegistryOps.Write(c.Hive, c.Path, c.Name, c.OriginalValue, c.OriginalKind);
                }
                else
                {
                    // Value didn't exist before — remove what we wrote
                    if (c.DeleteValue || c.DeleteKey) continue; // key/value was deleted and didn't exist → no-op
                    try { RegistryOps.DeleteValue(c.Hive, c.Path, c.Name); } catch { }
                }
            }

            state.AppliedTweaks.Remove(rec);
            state.Save();
            CatalogChanged?.Invoke();
            return (true, "Ajuste deshecho correctamente.");
        }
        catch (Exception ex)
        {
            return (false, $"Error al deshacer: {ex.Message}");
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    internal static string? GetActivePowerScheme()
    {
        var (_, stdout, _) = ProcessRunner.Run("powercfg", "/getactivescheme");
        var m = Regex.Match(stdout, @"([0-9a-f\-]{36})");
        return m.Success ? m.Groups[1].Value : null;
    }
}

// ─── Full tweak catalog ────────────────────────────────────────────────────

public static class TweakCatalog
{
    public static IReadOnlyList<Tweak> All { get; } = BuildAll();

    private static IReadOnlyList<Tweak> BuildAll()
    {
        return new List<Tweak>
        {
            // ═══════════════════════ RENDIMIENTO ═══════════════════════════════

            new()
            {
                Id = "perf-animations",
                Category = TweakCategory.Performance,
                Title = "Desactivar animaciones visuales",
                Description = "Quita las animaciones de ventanas, menús y transiciones. El equipo se siente más rápido, sobre todo en computadoras antiguas o con poco RAM.",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", Name = "VisualFXSetting", NewValue = 2 }
                }
            },
            new()
            {
                Id = "perf-transparency",
                Category = TweakCategory.Performance,
                Title = "Desactivar transparencias",
                Description = "Evita que Windows use efectos translúcidos en menús y barras, liberando recursos de la tarjeta gráfica.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", Name = "EnableTransparency", NewValue = 0 }
                }
            },
            new()
            {
                Id = "perf-startupdelay",
                Category = TweakCategory.Performance,
                Title = "Eliminar retraso de inicio de apps",
                Description = "Windows espera unos milisegundos antes de abrir cada app que se ejecuta al iniciar. Eliminar esa espera arranca todo más rápido.",
                Risk = RiskLevel.Low,
                NeedsRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", Name = "StartupDelayInMSec", NewValue = 0 }
                }
            },
            new()
            {
                Id = "perf-backgroundapps",
                Category = TweakCategory.Performance,
                Title = "Desactivar apps en segundo plano",
                Description = "Impide que las apps se actualicen o reciban datos cuando no están abiertas. Algunas (correo, calendario, clima) dejarán de avisarte hasta que las abras.",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", Name = "GlobalUserDisabled", NewValue = 1 }
                }
            },
            new()
            {
                Id = "perf-tips",
                Category = TweakCategory.Performance,
                Title = "Desactivar sugerencias y consejos de Windows",
                Description = "Oculta las notificaciones promocionales, consejos y tips que Windows muestra periódicamente.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name = "SoftLandingEnabled", NewValue = 0 },
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name = "SubscribedContent-338389Enabled", NewValue = 0 },
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name = "SubscribedContent-338388Enabled", NewValue = 0 }
                }
            },
            new()
            {
                Id = "perf-priority",
                Category = TweakCategory.Performance,
                Title = "Prioridad a programas en primer plano",
                Description = "Le da más recursos a los programas que estás usando (juegos, navegador) y menos a los servicios que corren en segundo plano.",
                Risk = RiskLevel.Medium,
                NeedsRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SYSTEM\CurrentControlSet\Control\PriorityControl", Name = "Win32PrioritySeparation", NewValue = 38, Kind = RegistryValueKind.DWord }
                }
            },
            new()
            {
                Id = "perf-errors",
                Category = TweakCategory.Performance,
                Title = "Ocultar avisos de errores de apps",
                Description = "Windows dejará de preguntarte si quieres enviar informes cuando una app falla o se cierra inesperadamente.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\Windows Error Reporting", Name = "DontShowUI", NewValue = 1 }
                }
            },

            // ═══════════════════════ PRIVACIDAD ═══════════════════════════════

            new()
            {
                Id = "priv-telemetry",
                Category = TweakCategory.Privacy,
                Title = "Reducir telemetría",
                Description = "Reduce la cantidad de datos de diagnóstico que Windows envía a Microsoft (comportamiento, uso, errores).",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", Name = "AllowTelemetry", NewValue = 0 }
                }
            },
            new()
            {
                Id = "priv-adsid",
                Category = TweakCategory.Privacy,
                Title = "Desactivar ID de publicidad",
                Description = "Windows deja de asignarte un identificador único para mostrarte anuncios personalizados en apps y el sistema.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", Name = "Enabled", NewValue = 0 }
                }
            },
            new()
            {
                Id = "priv-activity",
                Category = TweakCategory.Privacy,
                Title = "Desactivar historial de actividad",
                Description = "Windows deja de guardar qué apps y archivos usas para la línea de tiempo y la sincronización entre dispositivos.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Windows\System", Name = "EnableActivityFeed", NewValue = 0 }
                }
            },
            new()
            {
                Id = "priv-cortana",
                Category = TweakCategory.Privacy,
                Title = "Desactivar Cortana",
                Description = "Desactiva el asistente de voz y búsqueda inteligente de Cortana.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", Name = "AllowCortana", NewValue = 0 }
                }
            },
            new()
            {
                Id = "priv-bing",
                Category = TweakCategory.Privacy,
                Title = "Quitar búsqueda web del menú Inicio",
                Description = "Los resultados de búsqueda en el menú Inicio dejarán de incluir resultados de internet, anuncios y sugerencias web.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Search", Name = "BingSearchEnabled", NewValue = 0 }
                }
            },
            new()
            {
                Id = "priv-suggestions",
                Category = TweakCategory.Privacy,
                Title = "Desactivar apps sugeridas en Inicio",
                Description = "Windows deja de sugerirte apps que podrían interesarte en el menú Inicio.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name = "SubscribedContent-310093Enabled", NewValue = 0 },
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name = "SystemPaneSuggestionsEnabled", NewValue = 0 }
                }
            },
            new()
            {
                Id = "priv-tailored",
                Category = TweakCategory.Privacy,
                Title = "Desactivar experiencias personalizadas",
                Description = "Windows deja de usar tus datos de diagnóstico para mostrarte ofertas, tips y recomendaciones personalizadas.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Privacy", Name = "TailoredExperiencesWithDiagnosticDataEnabled", NewValue = 0 }
                }
            },
            new()
            {
                Id = "priv-finddevice",
                Category = TweakCategory.Privacy,
                Title = "Desactivar \"Encontrar mi dispositivo\"",
                Description = "Windows deja de sincronizar la ubicación de tu equipo. Ya no podrás ubicarlo desde tu cuenta Microsoft.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Settings\FindMyDevice", Name = "LocationSyncEnabled", NewValue = 0 }
                }
            },
            new()
            {
                Id = "priv-location",
                Category = TweakCategory.Privacy,
                Title = "Denegar acceso a la ubicación",
                Description = "Las apps y Windows no podrán conocer tu ubicación hasta que lo permitas de nuevo en Configuración.",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", Name = "Value", NewValue = "Deny", Kind = RegistryValueKind.String }
                }
            },
            new()
            {
                Id = "priv-lockscreen",
                Category = TweakCategory.Privacy,
                Title = "Desactivar sugerencias en pantalla de bloqueo",
                Description = "Quita el \"Spotlight\" y las sugerencias promocionales de la pantalla de bloqueo; usarás un fondo fijo.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name = "RotatingLockScreenEnabled", NewValue = 0 },
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name = "RotatingLockScreenOverlayEnabled", NewValue = 0 }
                }
            },

            // ═══════════════════════ GPU Y GRÁFICOS ═══════════════════════════

            new()
            {
                Id = "gpu-mpo",
                Category = TweakCategory.Gpu,
                Title = "Desactivar MPO (parpadeo de pantalla)",
                Description = "Desactiva la superposición de múltiples planos de imagen (MPO), que puede causar parpadeos y microtartamudeos en algunas tarjetas gráficas.",
                Risk = RiskLevel.Medium,
                NeedsRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows\Dwm", Name = "OverlayTestMode", NewValue = 5 }
                }
            },
            new()
            {
                Id = "gpu-hags",
                Category = TweakCategory.Gpu,
                Title = "Activar aceleración de GPU por hardware",
                Description = "Activa la programación de GPU acelerada por hardware (HAGS) para reducir la latencia en juegos y apps gráficas. Requiere reinicio y una GPU compatible.",
                Risk = RiskLevel.High,
                NeedsRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", Name = "HwSchMode", NewValue = 2 }
                }
            },
            new()
            {
                Id = "gpu-fso",
                Category = TweakCategory.Gpu,
                Title = "Desactivar optimizaciones de pantalla completa",
                Description = "Evita que Windows optimice automáticamente los juegos a pantalla completa. Útil si tienes caídas de FPS o el Alt-Tab es lento.",
                Risk = RiskLevel.Medium,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"System\GameConfigStore", Name = "GameDVR_FSEBehaviorMode", NewValue = 2 },
                    new() { Hive = "HKCU", Path = @"System\GameConfigStore", Name = "GameDVR_FSEBehavior", NewValue = 2 }
                }
            },

            // ═══════════════════════ ENERGÍA ═══════════════════════════════════

            new()
            {
                Id = "power-highperf",
                Category = TweakCategory.Power,
                Title = "Plan de alto rendimiento",
                Description = "Cambia al plan de energía \"Alto rendimiento\". La PC será más rápida pero puede gastar más batería en laptops.",
                Risk = RiskLevel.Medium,
                CustomApply = ctx =>
                {
                    string? prev = TweakEngine.GetActivePowerScheme();
                    ctx.CustomData["prevScheme"] = prev ?? "";
                    var (code, _, err) = ProcessRunner.Run("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                    if (code != 0) return (false, $"No se pudo cambiar el plan: {err}");
                    return (true, "Plan de energía cambiado a Alto rendimiento.");
                },
                CustomUndo = ctx =>
                {
                    if (ctx.CustomData.TryGetValue("prevScheme", out var prev) && !string.IsNullOrEmpty(prev))
                        ProcessRunner.Run("powercfg", $"/setactive {prev}");
                    return (true, "Plan de energía restaurado.");
                }
            },
            new()
            {
                Id = "power-ultimate",
                Category = TweakCategory.Power,
                Title = "Plan de rendimiento máximo",
                Description = "Crea y activa el plan \"Rendimiento máximo\" (oculto por Windows). Ofrece el máximo rendimiento posible, pero la batería durará menos.",
                Risk = RiskLevel.High,
                CustomApply = ctx =>
                {
                    string? prev = TweakEngine.GetActivePowerScheme();
                    ctx.CustomData["prevScheme"] = prev ?? "";

                    var (code, stdout, err) = ProcessRunner.Run("powercfg",
                        "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
                    var m = Regex.Match(stdout, @"([0-9a-f\-]{36})");
                    if (!m.Success) return (false, $"No se pudo crear el plan: {err}");

                    string newScheme = m.Groups[1].Value;
                    ctx.CustomData["newScheme"] = newScheme;
                    var (code2, _, err2) = ProcessRunner.Run("powercfg", $"/setactive {newScheme}");
                    if (code2 != 0) return (false, $"Plan creado pero no se pudo activar: {err2}");

                    return (true, "Plan \"Rendimiento máximo\" creado y activado.");
                },
                CustomUndo = ctx =>
                {
                    if (ctx.CustomData.TryGetValue("prevScheme", out var prev) && !string.IsNullOrEmpty(prev))
                        ProcessRunner.Run("powercfg", $"/setactive {prev}");
                    if (ctx.CustomData.TryGetValue("newScheme", out var ns) && !string.IsNullOrEmpty(ns))
                        ProcessRunner.Run("powercfg", $"-deletescheme {ns}");
                    return (true, "Plan \"Rendimiento máximo\" eliminado y plan anterior restaurado.");
                }
            },
            new()
            {
                Id = "power-faststartup",
                Category = TweakCategory.Power,
                Title = "Desactivar inicio rápido",
                Description = "El inicio rápido usa el archivo de hibernación para arrancar más rápido. Desactivarlo puede solucionar problemas con hibernación, doble arranque o actualizaciones.",
                Risk = RiskLevel.Low,
                NeedsRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", Name = "HiberbootEnabled", NewValue = 0 }
                }
            },
            new()
            {
                Id = "power-usbsuspend",
                Category = TweakCategory.Power,
                Title = "Desactivar suspensión selectiva de USB",
                Description = "Los dispositivos USB (teclados, ratones, adaptadores Wi-Fi) nunca se suspenderán para ahorrar energía. Evita desconexiones o reinicios inesperados de dispositivos.",
                Risk = RiskLevel.Low,
                CustomApply = ctx =>
                {
                    ProcessRunner.Run("powercfg", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
                    ProcessRunner.Run("powercfg", "/setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
                    ProcessRunner.Run("powercfg", "/setactive SCHEME_CURRENT");
                    return (true, "Suspensión USB desactivada (enchufado y batería).");
                },
                CustomUndo = ctx =>
                {
                    ProcessRunner.Run("powercfg", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1");
                    ProcessRunner.Run("powercfg", "/setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1");
                    ProcessRunner.Run("powercfg", "/setactive SCHEME_CURRENT");
                    return (true, "Suspensión USB restaurada.");
                }
            },
            new()
            {
                Id = "power-monitor",
                Category = TweakCategory.Power,
                Title = "La pantalla nunca se apaga (enchufado)",
                Description = "Cuando el PC está enchufado, la pantalla no se apagará por inactividad. Útil en monitores y presentaciones.",
                Risk = RiskLevel.Low,
                CustomApply = ctx =>
                {
                    var (_, outPrev, _) = ProcessRunner.Run("powercfg", "/query SCHEME_CURRENT SUB_VIDEO VIDEOIDLE");
                    var m = Regex.Match(outPrev, @"Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)");
                    ctx.CustomData["prevAC"] = m.Success ? m.Groups[1].Value : "3c";
                    ProcessRunner.Run("powercfg", "/change monitor-timeout-ac 0");
                    return (true, "Pantalla configurada para nunca apagarse (enchufado).");
                },
                CustomUndo = ctx =>
                {
                    string hex = ctx.CustomData.TryGetValue("prevAC", out var v) ? v : "3c";
                    if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int dec))
                        ProcessRunner.Run("powercfg", $"/change monitor-timeout-ac {dec}");
                    else
                        ProcessRunner.Run("powercfg", "/change monitor-timeout-ac 30");
                    return (true, "Tiempo de espera de pantalla restaurado.");
                }
            },

            // ═══════════════════════ ESCRITORIO ════════════════════════════════

            new()
            {
                Id = "desk-hidden",
                Category = TweakCategory.Desktop,
                Title = "Mostrar archivos y carpetas ocultos",
                Description = "El Explorador mostrará archivos ocultos y archivos del sistema. Útil para solucionar problemas o acceder a configuraciones avanzadas.",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "Hidden", NewValue = 1 },
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "ShowSuperHidden", NewValue = 1 }
                }
            },
            new()
            {
                Id = "desk-extensions",
                Category = TweakCategory.Desktop,
                Title = "Mostrar extensiones de archivos",
                Description = "Verás la extensión de cada archivo (.txt, .jpg, .exe…). Ayuda a reconocer archivos potencialmente peligrosos disfrazados.",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "HideFileExt", NewValue = 0 }
                }
            },
            new()
            {
                Id = "desk-thispc",
                Category = TweakCategory.Desktop,
                Title = "Mostrar \"Este equipo\" en el escritorio",
                Description = "Añade el icono de \"Este equipo\" (Mi PC) en tu escritorio para acceder rápido a tus discos.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{20D04FE0-3AEA-1069-A2D8-08002B30309D}" },
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", Name = "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", NewValue = 0 }
                }
            },
            new()
            {
                Id = "desk-contextmenu",
                Category = TweakCategory.Desktop,
                Title = "Restaurar menú clásico de clic derecho",
                Description = "Restaura el menú contextual completo del clic derecho, como en Windows 10. Solo afecta a Windows 11; en Windows 10 no tiene efecto.",
                Risk = RiskLevel.Medium,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new()
                    {
                        Hive = "HKCU",
                        Path = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
                        Name = "",
                        NewValue = "",
                        Kind = RegistryValueKind.String,
                        KeyDefaultValue = ""
                    }
                }
            },

            // ═══════════════════════ BARRA DE TAREAS ═══════════════════════════

            new()
            {
                Id = "tb-align",
                Category = TweakCategory.Taskbar,
                Title = "Alinear iconos a la izquierda",
                Description = "Cambia la alineación de los iconos de la barra de tareas de centro a izquierda (estilo Windows 10). Solo Windows 11.",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "TaskbarAl", NewValue = 0 }
                }
            },
            new()
            {
                Id = "tb-seconds",
                Category = TweakCategory.Taskbar,
                Title = "Mostrar segundos en el reloj",
                Description = "El reloj de la bandeja del sistema mostrará también los segundos. Windows 11 22H2 o posterior.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "ShowSecondsInSystemClock", NewValue = 1 }
                }
            },
            new()
            {
                Id = "tb-news",
                Category = TweakCategory.Taskbar,
                Title = "Ocultar Noticias e intereses / Widgets",
                Description = "Quita el botón de noticias (Windows 10) o de widgets (Windows 11) de la barra de tareas.",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Feeds", Name = "ShellFeedsTaskbarViewMode", NewValue = 0 }
                }
            },
            new()
            {
                Id = "tb-taskview",
                Category = TweakCategory.Taskbar,
                Title = "Ocultar el botón \"Vista de tareas\"",
                Description = "Quita el botón de vista de tareas (icono de dos rectángulos) de la barra de tareas.",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "ShowTaskViewButton", NewValue = 0 }
                }
            },
            new()
            {
                Id = "tb-chat",
                Category = TweakCategory.Taskbar,
                Title = "Ocultar el botón de Chat (Teams)",
                Description = "Quita el botón de chat de Microsoft Teams de la barra de tareas (Windows 11).",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "TaskbarMn", NewValue = 0 }
                }
            },
            new()
            {
                Id = "tb-search",
                Category = TweakCategory.Taskbar,
                Title = "Mostrar solo icono de búsqueda",
                Description = "Reduce la caja de búsqueda a un simple icono en la barra de tareas (Windows 11).",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "SearchboxTaskbarMode", NewValue = 1 }
                }
            },

            // ═══════════════════════ EXPLORADOR ════════════════════════════════

            new()
            {
                Id = "exp-thispc",
                Category = TweakCategory.Explorer,
                Title = "Abrir Explorador en \"Este equipo\"",
                Description = "Al abrir el Explorador de archivos verás tus discos y carpetas principales en lugar de la página de Inicio rápido.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "LaunchTo", NewValue = 1 }
                }
            },
            new()
            {
                Id = "exp-separate",
                Category = TweakCategory.Explorer,
                Title = "Abrir cada carpeta en su propio proceso",
                Description = "Si el Explorador falla, solo se cierra la ventana afectada. Más estable pero usa un poco más de memoria.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "SeparateProcess", NewValue = 1 }
                }
            },
            new()
            {
                Id = "exp-recent",
                Category = TweakCategory.Explorer,
                Title = "Ocultar archivos recientes y frecuentes",
                Description = "El Explorador dejará de mostrar los archivos y carpetas que abres frecuentemente en la vista rápida.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "ShowRecent", NewValue = 0 },
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "ShowFrequent", NewValue = 0 }
                }
            },
            new()
            {
                Id = "exp-tray",
                Category = TweakCategory.Explorer,
                Title = "Mostrar todos los iconos de la bandeja",
                Description = "La bandeja del sistema mostrará siempre todos los iconos de apps, sin esconder ninguno detrás de la flecha.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer", Name = "EnableAutoTray", NewValue = 1 }
                }
            },
            new()
            {
                Id = "exp-ads",
                Category = TweakCategory.Explorer,
                Title = "Quitar avisos promocionales del Explorador",
                Description = "Oculta los avisos y sugerencias promocionales de OneDrive y otras apps dentro del Explorador (Windows 11).",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "ShowSyncProviderNotifications", NewValue = 0 }
                }
            },

            // ═══════════════════════ JUEGOS ════════════════════════════════════

            new()
            {
                Id = "game-mode",
                Category = TweakCategory.Gaming,
                Title = "Activar Modo juego",
                Description = "Windows prioriza los recursos cuando estás en un juego y reduce notificaciones para que no te interrumpan.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\GameBar", Name = "AutoGameModeEnabled", NewValue = 1 }
                }
            },
            new()
            {
                Id = "game-bar-off",
                Category = TweakCategory.Gaming,
                Title = "Desactivar Xbox Game Bar",
                Description = "Desactiva la superolución de la barra de juegos de Xbox (Win+G). Puede ganarte unos FPS en juegos.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\GameBar", Name = "AppCaptureEnabled", NewValue = 0 },
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\GameDVR", Name = "AppCaptureEnabled", NewValue = 0 }
                }
            },
            new()
            {
                Id = "game-dvr",
                Category = TweakCategory.Gaming,
                Title = "Desactivar grabación en segundo plano",
                Description = "Windows deja de grabar automáticamente clips y capturas de tus juegos en segundo plano, liberando recursos.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"System\GameConfigStore", Name = "GameDVR_Enabled", NewValue = 0 },
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", Name = "AllowGameDVR", NewValue = 0 }
                }
            },
            new()
            {
                Id = "game-nagle",
                Category = TweakCategory.Gaming,
                Title = "Reducir latencia de red en juegos",
                Description = "Cambia configuración TCP/IP para que los paquetes de red en juegos online se envíen más rápido. Puede aumentar ligeramente el uso de datos.",
                Risk = RiskLevel.Medium,
                CustomApply = ctx =>
                {
                    string tcpipPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
                    string tcpipHive = "HKLM";
                    var (v, _, _) = RegistryOps.Read(tcpipHive, tcpipPath, "");
                    // List subkeys
                    using var root = Registry.LocalMachine;
                    using var key = root.OpenSubKey(tcpipPath, true);
                    if (key == null) return (false, "No se encontraron interfaces de red.");

                    var interfaces = new List<string>();
                    var items = new List<NagleEntry>();
                    foreach (string subName in key.GetSubKeyNames())
                    {
                        using var sub = key.OpenSubKey(subName, true);
                        if (sub == null) continue;
                        interfaces.Add(subName);

                        bool hasAck = sub.GetValue("TcpAckFrequency") != null;
                        bool hasNoDelay = sub.GetValue("TCPNoDelay") != null;
                        int origAck = hasAck ? Convert.ToInt32(sub.GetValue("TcpAckFrequency")) : -1;
                        int origNoDelay = hasNoDelay ? Convert.ToInt32(sub.GetValue("TCPNoDelay")) : -1;
                        items.Add(new NagleEntry { InterfaceName = subName, HasTcpAckFrequency = hasAck, OrigTcpAckFrequency = origAck, HasTCPNoDelay = hasNoDelay, OrigTCPNoDelay = origNoDelay });

                        sub.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                        sub.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                    }
                    ctx.CustomData["nagle"] = System.Text.Json.JsonSerializer.Serialize(items);
                    return (true, "Latencia de red reducida en todas las interfaces de red.");
                },
                CustomUndo = ctx =>
                {
                    if (!ctx.CustomData.TryGetValue("nagle", out var json)) return (false, "No hay datos guardados para deshacer.");

                    var items = System.Text.Json.JsonSerializer.Deserialize<List<NagleEntry>>(json) ?? new();
                    string tcpipPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
                    using var root = Registry.LocalMachine;
                    using var key = root.OpenSubKey(tcpipPath, true);
                    if (key == null) return (false, "No se encontraron interfaces.");

                    foreach (var item in items)
                    {
                        using var sub = key.OpenSubKey(item.InterfaceName, true);
                        if (sub == null) continue;
                        if (item.OrigTcpAckFrequency >= 0) sub.SetValue("TcpAckFrequency", item.OrigTcpAckFrequency, RegistryValueKind.DWord);
                        else sub.DeleteValue("TcpAckFrequency", false);
                        if (item.OrigTCPNoDelay >= 0) sub.SetValue("TCPNoDelay", item.OrigTCPNoDelay, RegistryValueKind.DWord);
                        else sub.DeleteValue("TCPNoDelay", false);
                    }
                    return (true, "Configuración de red restaurada.");
                }
            },

            // ═══════════════════════ SISTEMA ═══════════════════════════════════

            new()
            {
                Id = "sys-autoend",
                Category = TweakCategory.System,
                Title = "Cerrar apps automáticamente al apagar",
                Description = "Windows no esperará a que cada app te pida confirmación al apagar o reiniciar. Guarda tu trabajo antes de apagar.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Control Panel\Desktop", Name = "AutoEndTasks", NewValue = 1, Kind = RegistryValueKind.String }
                }
            },
            new()
            {
                Id = "sys-waittokill",
                Category = TweakCategory.System,
                Title = "Apagar y reiniciar más rápido",
                Description = "Windows esperará menos tiempo a las apps que no responden antes de cerrarlas al apagar.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Control Panel\Desktop", Name = "WaitToKillAppTimeout", NewValue = "2000", Kind = RegistryValueKind.String },
                    new() { Hive = "HKCU", Path = @"Control Panel\Desktop", Name = "HungAppTimeout", NewValue = "2000", Kind = RegistryValueKind.String }
                }
            },
            new()
            {
                Id = "sys-dodownload",
                Category = TweakCategory.System,
                Title = "Quitar uso de datos P2P de Windows Update",
                Description = "Windows deja de compartir tus descargas de actualizaciones con otros PCs de internet. Ahorra ancho de banda.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", Name = "DODownloadMode", NewValue = 0 }
                }
            },
            new()
            {
                Id = "sys-copilot",
                Category = TweakCategory.System,
                Title = "Desactivar Copilot de Windows",
                Description = "Desactiva el asistente Copilot integrado en Windows 11. Libera memoria y evita que aparezca en el menú.",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Policies\Microsoft\Windows\WindowsCopilot", Name = "TurnOffWindowsCopilot", NewValue = 1 }
                }
            },

            // ═══════════════════════ RENDIMIENTO (extras) ═══════════════════════

            new()
            {
                Id = "perf-maintenance",
                Category = TweakCategory.Performance,
                Title = "Desactivar tareas automáticas de mantenimiento",
                Description = "Detiene Windows de ejecutar tareas de mantenimiento programadas (desfragmentación, limpieza, etc.) que pueden ralentizar el equipo.",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance", Name = "MaintenanceDisabled", NewValue = 1 }
                }
            },
            new()
            {
                Id = "perf-diagnostics",
                Category = TweakCategory.Performance,
                Title = "Reducir servicios de diagnóstico",
                Description = "Desactiva servicios de diagnóstico y monitoreo que consumen recursos en segundo plano (DiagTrack, dmwappushservice).",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", Name = "AllowTelemetry", NewValue = 0 },
                    new() { Hive = "HKLM", Path = @"SYSTEM\CurrentControlSet\Services\DiagTrack", Name = "Start", NewValue = 4 },
                    new() { Hive = "HKLM", Path = @"SYSTEM\CurrentControlSet\Services\dmwappushservice", Name = "Start", NewValue = 4 }
                }
            },
            new()
            {
                Id = "perf-sysmain",
                Category = TweakCategory.Performance,
                Title = "Desactivar Superfetch / SysMain",
                Description = "Desactiva SysMain (antes Superfetch) que precarga apps en RAM. Puede mejorar rendimiento en discos SSD.",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SYSTEM\CurrentControlSet\Services\SysMain", Name = "Start", NewValue = 4 }
                }
            },
            new()
            {
                Id = "perf-defrag",
                Category = TweakCategory.Performance,
                Title = "Desactivar desfragmentación automática",
                Description = "Impide que Windows desfragmente los discos automáticamente. Útil si quieres controlar cuándo se ejecuta.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\Microsoft\Windows\Defrag", Name = "Id", DeleteValue = true }
                }
            },

            // ═══════════════════════ PRIVACIDAD (extras) ═══════════════════════

            new()
            {
                Id = "priv-publishuser",
                Category = TweakCategory.Privacy,
                Title = "Desactivar publicación de actividades",
                Description = "Windows deja de publicar tus actividades (qué abriste, qué editaste) para sincronizar entre dispositivos.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Windows\System", Name = "PublishUserActivities", NewValue = 0 },
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Windows\System", Name = "UploadUserActivities", NewValue = 0 }
                }
            },
            new()
            {
                Id = "priv-inputdiagnostic",
                Category = TweakCategory.Privacy,
                Title = "Desactivar recolección de datos de entrada",
                Description = "Windows deja de recopilar datos de escritura y voz para mejorar la experiencia de escritura.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\InputPersonalization", Name = "RestrictImplicitTextCollection", NewValue = 1 },
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\InputPersonalization", Name = "RestrictImplicitInkCollection", NewValue = 1 },
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\InputPersonalization\TrainedDataStore", Name = "HarvestContacts", NewValue = 0 }
                }
            },
            new()
            {
                Id = "priv-appdiagnostic",
                Category = TweakCategory.Privacy,
                Title = "Denegar acceso a datos de diagnóstico de apps",
                Description = "Las apps no podrán acceder a tus datos de diagnóstico, como información de errores y rendimiento.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics", Name = "Value", NewValue = "Deny", Kind = RegistryValueKind.String }
                }
            },
            new()
            {
                Id = "priv-webcam",
                Category = TweakCategory.Privacy,
                Title = "Denegar acceso a la cámara",
                Description = "Las apps no podrán usar tu cámara hasta que lo permitas de nuevo. No afecta a apps de videollamadas que ya uses.",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam", Name = "Value", NewValue = "Deny", Kind = RegistryValueKind.String }
                }
            },
            new()
            {
                Id = "priv-microphone",
                Category = TweakCategory.Privacy,
                Title = "Denegar acceso al micrófono",
                Description = "Las apps no podrán usar tu micrófono hasta que lo permitas de nuevo.",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone", Name = "Value", NewValue = "Deny", Kind = RegistryValueKind.String }
                }
            },
            new()
            {
                Id = "priv-consumer",
                Category = TweakCategory.Privacy,
                Title = "Desactivar funciones consumidor",
                Description = "Impide que Windows instale apps promocionales y sugerencias de la Tienda automáticamente.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", Name = "DisableWindowsConsumerFeatures", NewValue = 1 }
                }
            },
            new()
            {
                Id = "priv-scheduled",
                Category = TweakCategory.Privacy,
                Title = "Desactivar tareas programadas de telemetría",
                Description = "Detiene las tareas programadas que recopilan y envían datos de diagnóstico a Microsoft.",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser", Name = "Id", DeleteValue = true },
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\Microsoft\Windows\Application Experience\ProgramDataUpdater", Name = "Id", DeleteValue = true }
                }
            },
            new()
            {
                Id = "priv-onedrive-remove",
                Category = TweakCategory.Privacy,
                Title = "Eliminar OneDrive",
                Description = "Desinstala OneDrive por completo: cierra sus procesos, ejecuta el desinstalador oficial, quita su icono del Explorador y sus accesos de inicio. Tus archivos en la nube no se borran, siguen en onedrive.com. Requiere reiniciar el equipo para terminar.",
                Risk = RiskLevel.High,
                NeedsRestart = true,
                CustomApply = ctx =>
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string perUserFolder = Path.Combine(localAppData, @"Microsoft\OneDrive");

                    // ¿Está instalado el cliente de OneDrive? (OneDrive.exe o su desinstalador por usuario)
                    bool ClientFilesPresent() =>
                        File.Exists(Path.Combine(perUserFolder, "OneDrive.exe")) ||
                        File.Exists(Path.Combine(perUserFolder, "OneDriveSetup.exe"));

                    bool clientInstalled = ClientFilesPresent();

                    // 1) Localizar el desinstalador oficial de OneDrive
                    string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    string[] candidates =
                    {
                        Path.Combine(perUserFolder, "OneDriveSetup.exe"),
                        Path.Combine(winDir, @"SysWOW64\OneDriveSetup.exe"),
                        Path.Combine(winDir, @"System32\OneDriveSetup.exe"),
                        Path.Combine(progFiles, @"Microsoft OneDrive\OneDriveSetup.exe"),
                        Path.Combine(progFilesX86, @"Microsoft OneDrive\OneDriveSetup.exe")
                    };
                    string? setup = candidates.FirstOrDefault(File.Exists);

                    if (clientInstalled && setup == null)
                        return (false, "OneDrive está instalado pero no se encontró su desinstalador.");

                    // 2) Cerrar los procesos de OneDrive
                    foreach (var name in new[] { "OneDrive", "FileCoAuth" })
                    {
                        foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
                            p.Kill();
                    }

                    // 3) Ejecutar el desinstalador oficial (solo si el cliente está presente)
                    int code = 0;
                    bool uninstallerRan = false;
                    if (clientInstalled)
                    {
                        uninstallerRan = true;
                        (code, _, _) = ProcessRunner.Run(setup!, "/uninstall", 90_000);
                        // El desinstalador puede terminar en segundo plano; darle unos segundos
                        Thread.Sleep(3000);
                        clientInstalled = ClientFilesPresent();
                    }

                    // 4) Quitar el paquete Appx de OneDrive (Windows 11) si existiera
                    ProcessRunner.Run("powershell.exe",
                        "-NoProfile -NonInteractive -Command \"Get-AppxPackage -Name 'Microsoft.OneDriveSync' -AllUsers | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue\"",
                        30_000);

                    // 5) Si el desinstalador falló y el cliente sigue instalado, no tocar el registro
                    //    (así los datos para deshacer se guardan intactos si el usuario reintenta).
                    if (uninstallerRan && code != 0 && clientInstalled)
                        return (false, $"El desinstalador de OneDrive no pudo completarse (código {code}). Probá ejecutar Wachin como administrador.");

                    // 6) Quitar el icono de OneDrive del Explorador
                    const string clsid = @"Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}";
                    if (RegistryOps.KeyExists("HKCU", clsid))
                        RegistryOps.DeleteKey("HKCU", clsid);

                    // 7) Quitar OneDrive de los programas de inicio (guardando el estado previo)
                    var (runVal, runKind, runExists) = RegistryOps.Read("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", "OneDrive");
                    if (runExists)
                    {
                        ctx.CustomData["runValue"] = Convert.ToString(runVal) ?? "";
                        ctx.CustomData["runKind"] = runKind.ToString();
                        RegistryOps.DeleteValue("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", "OneDrive");
                    }
                    var (apprVal, _, apprExists) = RegistryOps.Read("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "OneDrive");
                    if (apprExists && apprVal is byte[] apprBytes)
                    {
                        ctx.CustomData["apprValue"] = Convert.ToBase64String(apprBytes);
                        RegistryOps.DeleteValue("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "OneDrive");
                    }

                    string msg;
                    if (!uninstallerRan)
                        msg = "OneDrive no estaba instalado. Se eliminaron sus accesos de inicio y del Explorador que habían quedado.";
                    else if (code == 0)
                        msg = "OneDrive desinstalado. Se quitaron su icono del Explorador y sus accesos de inicio. Reiniciá el equipo para terminar el proceso.";
                    else
                        msg = "OneDrive ya no está instalado. Se eliminaron sus accesos de inicio y del Explorador que habían quedado.";
                    return (true, msg);
                },
                CustomUndo = ctx =>
                {
                    // Restaurar los accesos de inicio que existían antes
                    if (ctx.CustomData.TryGetValue("runValue", out var run) && run.Length > 0)
                    {
                        var kind = ctx.CustomData.TryGetValue("runKind", out var k) &&
                                   Enum.TryParse<RegistryValueKind>(k, out var parsed) ? parsed : RegistryValueKind.String;
                        RegistryOps.Write("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", "OneDrive", run, kind);
                    }
                    if (ctx.CustomData.TryGetValue("apprValue", out var appr) && appr.Length > 0)
                    {
                        RegistryOps.Write("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "OneDrive",
                            Convert.FromBase64String(appr), RegistryValueKind.Binary);
                    }
                    return (true, "Accesos de inicio restaurados. OneDrive no se reinstala automáticamente: descargalo desde https://www.microsoft.com/onedrive");
                }
            },

            // ═══════════════════════ ENERGÍA (extras) ═══════════════════════════

            new()
            {
                Id = "power-hibernate",
                Category = TweakCategory.Power,
                Title = "Desactivar hibernación",
                Description = "Desactiva la hibernación y elimina el archivo hiberfil.sys, liberando espacio en disco. La PC no podrá hibernarse.",
                Risk = RiskLevel.Medium,
                NeedsRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", Name = "HibernateEnabled", NewValue = 0 },
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings", Name = "ShowHibernateOption", NewValue = 0 }
                }
            },
            new()
            {
                Id = "power-sleep",
                Category = TweakCategory.Power,
                Title = "Desactivar suspensión automática",
                Description = "El PC nunca entrará en suspensión automáticamente. Útil si descargas archivos grandes o ejecutas tareas largas.",
                Risk = RiskLevel.Low,
                CustomApply = ctx =>
                {
                    ProcessRunner.Run("powercfg", "/change standby-timeout-ac 0");
                    ProcessRunner.Run("powercfg", "/change standby-timeout-dc 0");
                    return (true, "Suspensión automática desactivada.");
                },
                CustomUndo = ctx =>
                {
                    ProcessRunner.Run("powercfg", "/change standby-timeout-ac 30");
                    ProcessRunner.Run("powercfg", "/change standby-timeout-dc 15");
                    return (true, "Suspensión automática restaurada (30 min AC, 15 min batería).");
                }
            },

            // ═══════════════════════ SISTEMA (extras) ═══════════════════════════

            new()
            {
                Id = "sys-utc",
                Category = TweakCategory.System,
                Title = "Usar hora UTC (dual boot Linux)",
                Description = "Cambia la hora del reloj a formato UTC. Soluciona problemas de hora incorrecta si tienes doble arranque con Linux.",
                Risk = RiskLevel.Low,
                NeedsRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", Name = "RealTimeIsUniversal", NewValue = 1, Kind = RegistryValueKind.QWord }
                }
            },
            new()
            {
                Id = "sys-clipboard",
                Category = TweakCategory.System,
                Title = "Limpiar historial del portapapeles",
                Description = "Borra todo el historial del portapapeles (Ctrl+H). Libera memoria y protege tu privacidad.",
                Risk = RiskLevel.Low,
                CustomApply = ctx =>
                {
                    ProcessRunner.Run("powershell.exe", "-NoProfile -Command \"Clear-Clipboard\"");
                    return (true, "Historial del portapapeles limpiado.");
                },
                CustomUndo = ctx => (true, "No hay nada que restaurar para el portapapeles.")
            },
            new()
            {
                Id = "sys-dns",
                Category = TweakCategory.System,
                Title = "Usar DNS público (Cloudflare 1.1.1.1)",
                Description = "Cambia el servidor DNS a Cloudflare (1.1.1.1), que es más rápido y privado que el DNS de tu proveedor de internet.",
                Risk = RiskLevel.Medium,
                CustomApply = ctx =>
                {
                    // Get active adapter
                    var (_, stdout, _) = ProcessRunner.Run("netsh", "interface show interface");
                    var lines = stdout.Split('\n');
                    string adapter = "Wi-Fi";
                    foreach (var line in lines)
                    {
                        if (line.Contains("Connected") && !line.Contains("loopback"))
                        {
                            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 4)
                                adapter = string.Join(" ", parts.Skip(3));
                            break;
                        }
                    }
                    ctx.CustomData["adapter"] = adapter;

                    // Save current DNS
                    var (_, dnsOut, _) = ProcessRunner.Run("netsh", $"interface ip show dns name=\"{adapter}\"");
                    ctx.CustomData["prevDNS"] = dnsOut;

                    ProcessRunner.Run("netsh", $"interface ip set dns name=\"{adapter}\" static 1.1.1.1");
                    ProcessRunner.Run("netsh", $"interface ip add dns name=\"{adapter}\" 1.0.0.1 index=2");
                    return (true, $"DNS cambiado a Cloudflare (1.1.1.1) en adaptador \"{adapter}\".");
                },
                CustomUndo = ctx =>
                {
                    string adapter = ctx.CustomData.TryGetValue("adapter", out var a) ? a : "Wi-Fi";
                    ProcessRunner.Run("netsh", $"interface ip set dns name=\"{adapter}\" dhcp");
                    return (true, "DNS restaurado a automático (DHCP).");
                }
            },
            new()
            {
                Id = "sys-remote",
                Category = TweakCategory.System,
                Title = "Desactivar Escritorio remoto",
                Description = "Desactiva la conexión de Escritorio remoto. Nadie podrá conectarse a tu PC remotamente a menos que lo actives.",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SYSTEM\CurrentControlSet\Control\Terminal Server", Name = "fDenyTSConnections", NewValue = 1 }
                }
            },
            new()
            {
                Id = "sys-uac",
                Category = TweakCategory.System,
                Title = "Reducir notificaciones UAC",
                Description = "El Control de cuentas de usuario (UAC) te avisará solo cuando apps intenten cambiar el sistema, no cuando tú lo hagas.",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", Name = "ConsentPromptBehaviorAdmin", NewValue = 5 }
                }
            },
            new()
            {
                Id = "sys-errorreport",
                Category = TweakCategory.System,
                Title = "Desactivar envío de informes de errores",
                Description = "Windows no enviará informes de errores a Microsoft cuando una app falle. Puede reducir uso de red y CPU.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting", Name = "Disabled", NewValue = 1 }
                }
            },
            new()
            {
                Id = "sys-onedrive",
                Category = TweakCategory.System,
                Title = "Desactivar inicio automático de OneDrive",
                Description = "OneDrive no se ejecutará automáticamente al iniciar Windows. Ahorra RAM y tiempo de arranque.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", Name = "OneDrive", NewValue = new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, Kind = RegistryValueKind.Binary }
                }
            },
            new()
            {
                Id = "sys-timeline",
                Category = TweakCategory.System,
                Title = "Desactivar Línea de tiempo",
                Description = "Desactiva la vista de actividad reciente (Win+Tab). Puede mejorar el rendimiento del Explorador.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Windows\System", Name = "EnableActivityFeed", NewValue = 0 }
                }
            },

            // ═══════════════════════ EXPLORADOR (extras) ════════════════════════

            new()
            {
                Id = "exp-ribbon",
                Category = TweakCategory.Explorer,
                Title = "Ocultar la cinta de opciones del Explorador",
                Description = "La cinta de opciones del Explorador (Inicio, Compartir, Ver) se ocultará automáticamente y se mostrará al hacer clic.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Ribbon", Name = "QatItems", NewValue = "" }
                }
            },
            new()
            {
                Id = "exp-darkmode",
                Category = TweakCategory.Explorer,
                Title = "Modo oscuro en el Explorador",
                Description = "Activa el modo oscuro solo en el Explorador de archivos (no en todo el sistema). Requiere reiniciar el Explorador.",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", Name = "AppsUseLightTheme", NewValue = 0 }
                }
            },
            new()
            {
                Id = "exp-fullpath",
                Category = TweakCategory.Explorer,
                Title = "Mostrar ruta completa en la barra de título",
                Description = "La barra de título del Explorador mostrará la ruta completa del directorio en lugar de solo el nombre de la carpeta.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState", Name = "FullPath", NewValue = 1 }
                }
            },

            // ═══════════════════════ BARRA DE TAREAS (extras) ═══════════════════

            new()
            {
                Id = "tb-small",
                Category = TweakCategory.Taskbar,
                Title = "Usar botones pequeños en la barra de tareas",
                Description = "Los iconos de la barra de tareas serán más pequeños, dejando más espacio para ventanas abiertas.",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "TaskbarSmallIcons", NewValue = 1 }
                }
            },
            new()
            {
                Id = "tb-labels",
                Category = TweakCategory.Taskbar,
                Title = "Mostrar etiquetas en la barra de tareas",
                Description = "Cada botón de la barra de tareas mostrará el nombre de la ventana, no solo el icono.",
                Risk = RiskLevel.Low,
                NeedsExplorerRestart = true,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name = "TaskbarGlomLevel", NewValue = 2 }
                }
            },

            // ═══════════════════════ JUEGOS (extras) ════════════════════════════

            new()
            {
                Id = "game-priority",
                Category = TweakCategory.Gaming,
                Title = "Prioridad alta para procesos de juegos",
                Description = "Windows asignará más recursos de CPU a los procesos marcados como juegos. Puede ayudar con caídas de FPS.",
                Risk = RiskLevel.Medium,
                Changes = new()
                {
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", Name = "GPU Priority", NewValue = 8 },
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", Name = "Priority", NewValue = 6 },
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", Name = "Scheduling Category", NewValue = "High", Kind = RegistryValueKind.String },
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", Name = "SFIO Priority", NewValue = "High", Kind = RegistryValueKind.String }
                }
            },
            new()
            {
                Id = "game-gamebar",
                Category = TweakCategory.Gaming,
                Title = "Desactivar Game Bar completamente",
                Description = "Desactiva la Game Bar y todas sus funciones (grabación, capturas, superposición). Libera recursos del sistema.",
                Risk = RiskLevel.Low,
                Changes = new()
                {
                    new() { Hive = "HKCU", Path = @"Software\Microsoft\GameBar", Name = "UseNexusForGameBarEnabled", NewValue = 0 },
                    new() { Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", Name = "AllowGameDVR", NewValue = 0 }
                }
            },
        };
    }
}

// Helper for Nagle undo serialization
internal sealed class NagleEntry
{
    public string InterfaceName { get; set; } = "";
    public bool HasTcpAckFrequency { get; set; }
    public int OrigTcpAckFrequency { get; set; }
    public bool HasTCPNoDelay { get; set; }
    public int OrigTCPNoDelay { get; set; }
}
