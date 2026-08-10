using System.ServiceProcess;
using Wachin.Models;

namespace Wachin.Services;

public static class ServiceManagerService
{
    private static readonly Dictionary<string, (string Desc, RiskLevel Risk)> Recommended = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SysMain"] = ("Cachea archivos en RAM para abrir apps más rápido. En HDD puede ralentizar.", RiskLevel.Medium),
        ["DiagTrack"] = ("Telemetría: envía datos de diagnóstico a Microsoft.", RiskLevel.Low),
        ["dmwappushservice"] = ("Servicio de empuje WAP para configuración remota.", RiskLevel.Low),
        ["WSearch"] = ("Indexador de Windows: busca archivos y contenido. Desactivarlo ralentiza las búsquedas.", RiskLevel.Medium),
        ["Spooler"] = ("Cola de impresión. Desactiva si no usas impresora.", RiskLevel.Medium),
        ["Fax"] = ("Servicio de fax. Casi nadie usa fax hoy.", RiskLevel.Low),
        ["XblAuthManager"] = ("Autenticación de Xbox Live.", RiskLevel.Low),
        ["XblGameSave"] = ("Guardado en la nube de Xbox.", RiskLevel.Low),
        ["XboxNetApiSvc"] = ("Red de Xbox.", RiskLevel.Low),
        ["WerSvc"] = ("Informes de errores de Windows.", RiskLevel.Low),
        ["RemoteRegistry"] = ("Permite modificar el registro a distancia. Desactívalo por seguridad.", RiskLevel.Low),
        ["HomeGroupListener"] = ("Escucha conexiones de grupo doméstico (obsoleto).", RiskLevel.Low),
        ["HomeGroupProvider"] = ("Provee grupo doméstico (obsoleto).", RiskLevel.Low),
        ["lfsvc"] = ("Servicio de geolocalización.", RiskLevel.Medium),
        ["MapsBroker"] = ("Administrador de mapas offline.", RiskLevel.Low),
        ["RetailDemo"] = ("Modo demo de tienda. Seguro de desactivar.", RiskLevel.Low),
        ["TabletInputService"] = ("Teclado táctil y escritura a mano.", RiskLevel.Low),
    };

    public static List<ServiceItem> GetAll(AppState state)
    {
        var result = new List<ServiceItem>();

        try
        {
            using var sc = new ServiceController();
            var services = ServiceController.GetServices();
            foreach (var svc in services)
            {
                try
                {
                    string startLabel = svc.StartType switch
                    {
                        ServiceStartMode.Automatic => "Automático",
                        ServiceStartMode.Manual => "Manual",
                        ServiceStartMode.Disabled => "Desactivado",
                        ServiceStartMode.Boot => "Arranque",
                        ServiceStartMode.System => "Sistema",
                        _ => "?"
                    };

                    var item = new ServiceItem
                    {
                        Name = svc.ServiceName,
                        DisplayName = svc.DisplayName,
                        State = svc.Status == ServiceControllerStatus.Running ? "En ejecución" : "Detenido",
                        StartType = (int)svc.StartType,
                        Description = ""
                    };

                    if (Recommended.TryGetValue(svc.ServiceName, out var rec))
                    {
                        item.IsRecommended = true;
                        item.Risk = rec.Risk;
                        item.Description = rec.Desc;
                        item.RecommendedDescription = rec.Desc;
                    }

                    result.Add(item);
                }
                catch
                {
                    // Skip individual services that can't be read
                }
            }
        }
        catch (Exception ex)
        {
            // ServiceController not available
            System.Diagnostics.Debug.WriteLine($"Error loading services: {ex.Message}");
        }

        // Apply saved changes from state
        foreach (var ch in state.ChangedServices)
        {
            var existing = result.FirstOrDefault(x => x.Name == ch.Name);
            if (existing != null)
            {
                existing.WasChanged = true;
                existing.OriginalStartType = ch.OriginalStartType;
                existing.StartType = 4; // Disabled
            }
        }

        return result
            .OrderByDescending(x => x.IsRecommended)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static (bool Ok, string Message) Disable(ServiceItem item, AppState state)
    {
        int original = ReadStartType(item.Name);
        WriteStartType(item.Name, 4); // Disabled

        state.ChangedServices.RemoveAll(x => x.Name == item.Name);
        item.WasChanged = true;
        item.OriginalStartType = original;
        item.StartType = 4;
        state.ChangedServices.Add(new ServiceItem
        {
            Name = item.Name, DisplayName = item.DisplayName,
            StartType = 4, OriginalStartType = original
        });
        state.Save();

        return (true, $"Servicio \"{item.DisplayName}\" desactivado.");
    }

    public static (bool Ok, string Message) Restore(ServiceItem item, AppState state)
    {
        int target = item.OriginalStartType ?? 3; // default Manual
        WriteStartType(item.Name, target);

        state.ChangedServices.RemoveAll(x => x.Name == item.Name);
        item.WasChanged = false;
        item.OriginalStartType = null;
        item.StartType = target;
        state.Save();

        return (true, $"Tipo de inicio de \"{item.DisplayName}\" restaurado.");
    }

    public static async Task<(bool Ok, string Message)> StartAsync(ServiceItem item)
    {
        try
        {
            using var sc = new ServiceController(item.Name);
            if (sc.Status == ServiceControllerStatus.Running)
                return (true, "Ya está en ejecución.");

            await Task.Run(() => sc.Start());
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            item.State = "En ejecución";
            return (true, $"{item.DisplayName} iniciado.");
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo iniciar: {ex.Message}");
        }
    }

    public static async Task<(bool Ok, string Message)> StopAsync(ServiceItem item)
    {
        try
        {
            using var sc = new ServiceController(item.Name);
            if (sc.Status == ServiceControllerStatus.Stopped)
                return (true, "Ya está detenido.");

            await Task.Run(() => sc.Stop());
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            item.State = "Detenido";
            return (true, $"{item.DisplayName} detenido.");
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo detener: {ex.Message}");
        }
    }

    private static int ReadStartType(string serviceName)
    {
        var (readVal, _, _) = RegistryOps.Read("HKLM", $@"SYSTEM\CurrentControlSet\Services\{serviceName}", "Start");
        return readVal is int i ? i : 3;
    }

    private static void WriteStartType(string serviceName, int value)
    {
        RegistryOps.WriteInt("HKLM", $@"SYSTEM\CurrentControlSet\Services\{serviceName}", "Start", value);
    }
}
