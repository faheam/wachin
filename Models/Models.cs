using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Wachin.Models;

public enum RiskLevel { Low, Medium, High }

public enum TweakCategory
{
    Performance, Privacy, Gpu, Power,
    Services, Bloatware, Desktop, Taskbar,
    Explorer, Gaming, System
}

public enum ConfirmResult { Cancel, Apply, RestoreAndApply }

// ─── Registry change descriptor ────────────────────────────────────────────

public sealed record RegistryChange
{
    /// <summary>Hive: "HKCU", "HKLM", or "HKLM32" for WOW6432Node.</summary>
    public string Hive { get; init; } = "HKCU";
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public RegistryValueKind Kind { get; init; } = RegistryValueKind.DWord;
    /// <summary>Value to write when the tweak is applied.</summary>
    public object? NewValue { get; init; }
    /// <summary>When true, apply = delete the value (or key if Path is the target).</summary>
    public bool DeleteValue { get; init; }
    /// <summary>When true, apply = delete the entire key at Path.</summary>
    public bool DeleteKey { get; init; }
    /// <summary>If DeleteKey, this default string value is used to recreate the key on undo (for the InprocServer32 trick).</summary>
    public string? KeyDefaultValue { get; init; }

    // ── captured original state (populated at apply time) ──
    public bool OriginalExists { get; set; }
    public object? OriginalValue { get; set; }
    public RegistryValueKind OriginalKind { get; set; }
}

// ─── Tweak definition ──────────────────────────────────────────────────────

public sealed class Tweak
{
    public required string Id { get; init; }
    public required TweakCategory Category { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public RiskLevel Risk { get; init; }
    public bool NeedsRestart { get; init; }
    public bool NeedsExplorerRestart { get; init; }

    public List<RegistryChange> Changes { get; init; } = new();

    /// <summary>Custom apply for non-registry tweaks. Returns (ok, message).</summary>
    public Func<TweakContext, (bool, string)>? CustomApply { get; init; }
    /// <summary>Custom undo for non-registry tweaks.</summary>
    public Func<TweakContext, (bool, string)>? CustomUndo { get; init; }
}

public sealed class TweakContext
{
    public required AppState State { get; init; }
    public required string TweakId { get; init; }
    public Dictionary<string, string> CustomData { get; set; } = new();
}

// ─── Applied tweak record (persisted in state.json) ────────────────────────

public sealed class AppliedTweak
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public TweakCategory Category { get; set; }
    public DateTime AppliedAt { get; set; }
    public List<RegistryChange>? Originals { get; set; }
    public Dictionary<string, string>? CustomData { get; set; }
}

// ─── Settings ──────────────────────────────────────────────────────────────

public sealed class AppSettings : INotifyPropertyChanged
{
    private bool _confirmBeforeApply = true;
    public bool ConfirmBeforeApply { get => _confirmBeforeApply; set { _confirmBeforeApply = value; OnPropertyChanged(); } }

    private bool _remindRestorePoint = true;
    public bool RemindRestorePoint { get => _remindRestorePoint; set { _remindRestorePoint = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ─── Tool entities ─────────────────────────────────────────────────────────

public sealed class StartupItem
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public string Source { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public string KeyId { get; set; } = "";
    // Registry info
    public string? RegHive { get; set; }
    public string? RegPath { get; set; }
    public string? RegName { get; set; }
    public object? RegValue { get; set; }
    // Folder info
    public string? FilePath { get; set; }
}

public sealed class ServiceItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    private string _state = "";
    public string State { get => _state; set { _state = value; OnPropertyChanged(); } }
    private int _startType = 3;
    public int StartType { get => _startType; set { _startType = value; OnPropertyChanged(); OnPropertyChanged(nameof(StartTypeLabel)); } }
    public string StartTypeLabel => StartType switch { 0 => "Arranque", 1 => "Sistema", 2 => "Automático", 3 => "Manual", 4 => "Desactivado", _ => "?" };
    public RiskLevel Risk { get; set; }
    public bool IsRecommended { get; set; }
    public string RecommendedDescription { get; set; } = "";
    private bool _wasChanged;
    public bool WasChanged { get => _wasChanged; set { _wasChanged = value; OnPropertyChanged(); } }
    public int? OriginalStartType { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class SchedTaskItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string State { get; set; } = "";
    public bool Enabled { get; set; }
    public string Action { get; set; } = "";
    public string Trigger { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsSystem { get; set; }
}

public sealed class BloatApp
{
    public string PackagePrefix { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public RiskLevel Risk { get; set; } = RiskLevel.Low;
    public bool Installed { get; set; }
    public string? FullName { get; set; }
    public string? Publisher { get; set; }
}

// ─── Programas (repo de apps esenciales) ───────────────────────────────────

public sealed class ProgramItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>ID oficial en winget. Si es null, se usa DownloadUrl o se abre la web oficial.</summary>
    public string? WingetId { get; init; }
    /// <summary>URL oficial directa del instalador (para apps sin winget).</summary>
    public string? DownloadUrl { get; init; }
    /// <summary>Argumentos de instalación silenciosa para DownloadUrl.</summary>
    public string? InstallArgs { get; init; }
    /// <summary>Página oficial del programa.</summary>
    public string? Homepage { get; init; }
    /// <summary>Tamaño estimado en MB (0 = desconocido).</summary>
    public long SizeMb { get; init; }
}

public sealed class CleanerCategory
{
    public string Id { get; init; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public RiskLevel Risk { get; init; } = RiskLevel.Low;
    public bool Selected { get; set; } = true;
    public List<string> Paths { get; init; } = new();
    public long SizeBytes { get; set; }
    public int ItemCount { get; set; }
    public int SkippedCount { get; set; }
    public bool SupportsSize { get; init; } = true;
    public string SizeLabel => SizeBytes switch
    {
        0 => "—",
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{SizeBytes / 1048576.0:F1} MB",
        _ => $"{SizeBytes / 1073741824.0:F2} GB"
    };
}

public sealed class SysInfo
{
    public string OsName = "", OsVersion = "", OsBuild = "", OsArch = "";
    public string CpuName = "", CpuCores = "", CpuThreads = "", CpuSpeed = "";
    public ulong RamTotal, RamUsed;
    public int RamPercent;
    public string GpuName = "", GpuVram = "", GpuDriver = "";
    public List<DiskInfo> Disks = new();
    public string? BatteryPercent, BatteryStatus;
    public string Motherboard = "", Bios = "", Uptime = "", MachineName = "";
}

public sealed class DiskInfo
{
    public string Id = "", Label = "";
    public ulong Total, Free;
    public string TotalLabel => FormatBytes(Total);
    public string FreeLabel => FormatBytes(Free);
    public double UsagePercent => Total > 0 ? (double)(Total - Free) / Total * 100 : 0;
    public static string FormatBytes(ulong b) => b switch
    {
        0 => "0 B",
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{b / 1048576.0:F1} MB",
        _ => $"{b / 1073741824.0:F1} GB"
    };
}

// ─── Toast ─────────────────────────────────────────────────────────────────

public enum ToastKind { Success, Info, Warning, Error }

public sealed class Toast
{
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public ToastKind Kind { get; init; }
    public string? ActionText { get; init; }
    public Action? Action { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

// ─── Nav item for sidebar ──────────────────────────────────────────────────

public sealed class NavItemVm : INotifyPropertyChanged
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Glyph { get; init; } = "";
    public bool IsHeader { get; init; }
    private int _totalTweaks;
    public int TotalTweaks { get => _totalTweaks; set { _totalTweaks = value; OnPropertyChanged(); OnPropertyChanged(nameof(BadgeText)); OnPropertyChanged(nameof(BadgeVisible)); } }
    private int _appliedCount;
    public int AppliedCount { get => _appliedCount; set { _appliedCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(BadgeText)); OnPropertyChanged(nameof(BadgeVisible)); } }
    public int Remaining => TotalTweaks - AppliedCount;
    public System.Windows.Visibility BadgeVisible => TotalTweaks > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public string BadgeText => TotalTweaks > 0 ? $"{Remaining}" : "";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
