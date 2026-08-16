using System.Runtime.InteropServices;
using Wachin.Models;

namespace Wachin.Services;

public static class ScheduledTaskService
{
    public static List<SchedTaskItem> GetAll()
    {
        var result = new List<SchedTaskItem>();
        try
        {
            Type? svcType = Type.GetTypeFromProgID("Schedule.Service");
            if (svcType == null) return result;

            dynamic svc = Activator.CreateInstance(svcType);
            svc.Connect();
            TraverseFolder(svc.GetFolder("\\"), result);
        }
        catch
        {
            // COM service not available
        }
        return result;
    }

    public static (bool Ok, string Message) SetEnabled(string taskPath, bool enable)
    {
        string arg = enable ? "enable" : "disable";
        var (code, stdout, stderr) = ProcessRunner.Run("schtasks.exe", $"/change /tn \"{taskPath}\" /{arg}");
        if (code == 0)
            return (true, enable ? "Tarea activada." : "Tarea desactivada.");

        string msg = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
        return (false, $"No se pudo cambiar la tarea: {msg}");
    }

    /// <summary>
    /// Estado actual de una tarea por su ruta. Devuelve true si existe y está activa,
    /// false si existe y está desactivada, o null si no existe / no se puede consultar.
    /// </summary>
    public static bool? GetEnabledState(string taskPath)
    {
        try
        {
            Type? svcType = Type.GetTypeFromProgID("Schedule.Service");
            if (svcType == null) return null;

            dynamic svc = Activator.CreateInstance(svcType);
            svc.Connect();

            string p = taskPath.TrimStart('\\');
            int idx = p.LastIndexOf('\\');
            string folderPath = idx >= 0 ? "\\" + p[..idx] : "\\";
            string name = idx >= 0 ? p[(idx + 1)..] : p;

            dynamic folder = svc.GetFolder(folderPath);
            dynamic task = folder.GetTask(name);
            return (bool)task.Enabled;
        }
        catch
        {
            return null;
        }
    }

    // ── Internals ──

    private static void TraverseFolder(dynamic folder, List<SchedTaskItem> result)
    {
        try
        {
            foreach (dynamic task in folder.GetTasks(0))
            {
                string path = task.Path ?? "";
                string name = task.Name ?? "";
                string desc = "";
                string action = "";
                string trigger = "";

                try
                {
                    dynamic def = task.Definition;
                    desc = def.RegistrationInfo?.Description ?? "";
                    if (def.Actions?.Count > 0)
                    {
                        dynamic a = def.Actions[1];
                        string cmd = a.Path ?? "";
                        string args = a.Arguments ?? "";
                        action = string.IsNullOrWhiteSpace(args) ? cmd : $"{cmd} {args}";
                    }
                    if (def.Triggers?.Count > 0)
                    {
                        dynamic t = def.Triggers[1];
                        if (t.StartBoundary != null)
                            trigger = Convert.ToDateTime(t.StartBoundary).ToString("g");
                        else
                            trigger = t.ToString() ?? "";
                    }
                }
                catch { }

                bool enabled = task.Enabled;
                int stateVal = task.State;
                string state = stateVal switch
                {
                    1 => "Desactivada",
                    2 => "En cola",
                    3 => "Lista",
                    4 => "En ejecución",
                    _ => "Desconocido"
                };

                bool isSystem = path.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);

                result.Add(new SchedTaskItem
                {
                    Name = name, Path = path, State = state,
                    Enabled = enabled, Action = action,
                    Trigger = trigger, Description = desc,
                    IsSystem = isSystem
                });
            }
        }
        catch { }

        try
        {
            foreach (dynamic sub in folder.GetFolders(0))
                TraverseFolder(sub, result);
        }
        catch { }
    }
}
