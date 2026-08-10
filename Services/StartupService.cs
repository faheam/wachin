using System.IO;
using Microsoft.Win32;
using Wachin.Models;

namespace Wachin.Services;

public static class StartupService
{
    private const string UserRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string UserRunOncePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";

    public static List<StartupItem> GetAll(AppState state)
    {
        var items = new List<StartupItem>();

        // ── Registry entries ──
        AddRegistry(items, "HKCU", UserRunPath, "Inicio (tu usuario)");
        AddRegistry(items, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Inicio (todos los usuarios)");
        AddRegistry(items, "HKLM32", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Inicio 32 bits");
        AddRegistry(items, "HKCU", UserRunOncePath, "Una sola vez (tu usuario)");

        // ── Startup folders ──
        AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Carpeta Inicio (tu usuario)");
        string common = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");
        AddFolder(items, common, "Carpeta Inicio (todos)");

        // Merge disabled items from state
        foreach (var disabled in state.DisabledStartup)
        {
            if (items.All(i => i.KeyId != disabled.KeyId))
                items.Add(disabled);
        }

        return items;
    }

    public static void Disable(StartupItem item, AppState state)
    {
        if (item.KeyId.StartsWith("reg:"))
        {
            // Delete registry value and save to state
            RegistryOps.DeleteValue(item.RegHive!, item.RegPath!, item.RegName!);
        }
        else if (item.KeyId.StartsWith("file:"))
        {
            // Move file to backup directory
            Directory.CreateDirectory(state.BackupDir);
            string dest = Path.Combine(state.BackupDir, Path.GetFileName(item.FilePath!));
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(item.FilePath!, dest);
        }

        item.IsEnabled = false;
        state.DisabledStartup.RemoveAll(d => d.KeyId == item.KeyId);
        state.DisabledStartup.Add(item);
        state.Save();
    }

    public static void Enable(StartupItem item, AppState state)
    {
        if (item.KeyId.StartsWith("reg:") && item.RegHive != null)
        {
            if (item.RegValue is not null)
                RegistryOps.Write(item.RegHive, item.RegPath!, item.RegName!, item.RegValue, RegistryValueKind.String);
        }
        else if (item.KeyId.StartsWith("file:"))
        {
            string backup = Path.Combine(state.BackupDir, Path.GetFileName(item.FilePath!));
            if (File.Exists(backup))
            {
                File.Move(backup, item.FilePath!, overwrite: true);
            }
        }

        item.IsEnabled = true;
        state.DisabledStartup.RemoveAll(d => d.KeyId == item.KeyId);
        state.Save();
    }

    public static void Delete(StartupItem item, AppState state)
    {
        if (item.KeyId.StartsWith("reg:"))
        {
            RegistryOps.DeleteValue(item.RegHive!, item.RegPath!, item.RegName!);
        }
        else if (item.KeyId.StartsWith("file:") && File.Exists(item.FilePath))
        {
            File.Delete(item.FilePath);
        }

        state.DisabledStartup.RemoveAll(d => d.KeyId == item.KeyId);
        state.Save();
    }

    // ── Internals ──

    private static void AddRegistry(List<StartupItem> items, string hive, string path, string source)
    {
        try
        {
            var values = RegistryOps.ReadValues(hive, path);
            foreach (var (name, value, _) in values)
            {
                string command = value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                items.Add(new StartupItem
                {
                    Name = name,
                    Command = command,
                    Source = source,
                    IsEnabled = true,
                    KeyId = $"reg:{hive}|{path}|{name}",
                    RegHive = hive,
                    RegPath = path,
                    RegName = name,
                    RegValue = value
                });
            }
        }
        catch { }
    }

    private static void AddFolder(List<StartupItem> items, string folderPath, string source)
    {
        if (!Directory.Exists(folderPath)) return;
        try
        {
            foreach (string file in Directory.GetFiles(folderPath))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                items.Add(new StartupItem
                {
                    Name = name,
                    Command = file,
                    Source = source,
                    IsEnabled = true,
                    KeyId = $"file:{file}",
                    FilePath = file
                });
            }
        }
        catch { }
    }
}
