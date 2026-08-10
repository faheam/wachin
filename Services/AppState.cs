using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wachin.Models;

public sealed class AppState
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string FilePath { get; }
    public string BackupDir { get; }
    public AppSettings Settings { get; set; } = new();
    public List<AppliedTweak> AppliedTweaks { get; set; } = new();
    public List<StartupItem> DisabledStartup { get; set; } = new();
    public List<ServiceItem> ChangedServices { get; set; } = new();

    public AppState(string filePath, string backupDir)
    {
        FilePath = filePath;
        BackupDir = backupDir;
    }

    // ── Persistence ────────────────────────────────────────────────────────

    public static AppState Load()
    {
        string dir = ResolveDataDir();
        string filePath = Path.Combine(dir, "state.json");
        string backupDir = Path.Combine(dir, "StartupBackup");
        Directory.CreateDirectory(dir);

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<AppState>(json, JsonOpts) ?? new AppState(filePath, backupDir);
            }
            catch
            {
                // Corrupted file — start fresh
                return new AppState(filePath, backupDir);
            }
        }
        return new AppState(filePath, backupDir);
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, JsonOpts);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, FilePath, overwrite: true);
        }
        catch { /* best effort */ }
    }

    private static string ResolveDataDir()
    {
        string exeDir = AppContext.BaseDirectory;
        try
        {
            string test = Path.Combine(exeDir, ".writetest");
            File.WriteAllText(test, "x");
            File.Delete(test);
            return exeDir;
        }
        catch
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Wachin");
        }
    }

    // ── Queries ────────────────────────────────────────────────────────────

    public bool IsApplied(string id) => AppliedTweaks.Any(a => a.Id == id);
    public AppliedTweak? GetApplied(string id) => AppliedTweaks.FirstOrDefault(a => a.Id == id);

    public int CountApplied(TweakCategory cat) =>
        AppliedTweaks.Count(a => a.Category == cat);

    public void ClearAll()
    {
        AppliedTweaks.Clear();
        DisabledStartup.Clear();
        ChangedServices.Clear();
        Save();
    }
}
