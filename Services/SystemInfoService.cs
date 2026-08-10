using System.Runtime.InteropServices;
using Microsoft.Management.Infrastructure;
using Wachin.Models;

namespace Wachin.Services;

public static class SystemInfoService
{
    private static IEnumerable<CimInstance> Query(string wql)
    {
        using var session = CimSession.Create(null);
        return session.QueryInstances(@"root\cimv2", "WQL", wql).ToList();
    }

    public static SysInfo Collect()
    {
        var info = new SysInfo { MachineName = Environment.MachineName };

        // ── OS ──
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            info.OsName = key?.GetValue("ProductName")?.ToString() ?? "Windows";
            info.OsVersion = key?.GetValue("DisplayVersion")?.ToString() ?? "";
            info.OsBuild = key?.GetValue("CurrentBuildNumber")?.ToString() ?? "";
            int.TryParse(key?.GetValue("UBR")?.ToString() ?? "0", out int ubr);
            if (!string.IsNullOrEmpty(info.OsBuild)) info.OsBuild += $".{ubr}";
        }
        catch
        {
            info.OsName = Environment.OSVersion.ToString();
        }
        info.OsArch = Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits";

        // ── CPU ──
        try
        {
            var cpu = Query("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor").FirstOrDefault();
            if (cpu != null)
            {
                info.CpuName = cpu.CimInstanceProperties["Name"]?.Value?.ToString()?.Trim() ?? "";
                info.CpuCores = cpu.CimInstanceProperties["NumberOfCores"]?.Value?.ToString() ?? "";
                info.CpuThreads = cpu.CimInstanceProperties["NumberOfLogicalProcessors"]?.Value?.ToString() ?? "";
                var speed = cpu.CimInstanceProperties["MaxClockSpeed"]?.Value;
                if (speed is uint mhz) info.CpuSpeed = $"{mhz} MHz";
            }
        }
        catch { }

        // ── RAM ──
        try
        {
            var mem = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ref mem))
            {
                info.RamTotal = mem.ullTotalPhys;
                info.RamUsed = mem.ullTotalPhys - mem.ullAvailPhys;
                info.RamPercent = (int)mem.dwMemoryLoad;
            }
        }
        catch
        {
            try
            {
                var cs = Query("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem").FirstOrDefault();
                var os = Query("SELECT FreePhysicalMemory FROM Win32_OperatingSystem").FirstOrDefault();
                if (cs != null && os != null)
                {
                    info.RamTotal = Convert.ToUInt64(cs.CimInstanceProperties["TotalPhysicalMemory"]?.Value);
                    info.RamUsed = info.RamTotal - Convert.ToUInt64(os.CimInstanceProperties["FreePhysicalMemory"]?.Value) * 1024;
                    info.RamPercent = info.RamTotal > 0 ? (int)((double)info.RamUsed / info.RamTotal * 100) : 0;
                }
            }
            catch { }
        }

        // ── GPU ──
        try
        {
            var gpu = Query("SELECT Name,AdapterRAM,DriverVersion FROM Win32_VideoController").FirstOrDefault();
            if (gpu != null)
            {
                info.GpuName = gpu.CimInstanceProperties["Name"]?.Value?.ToString()?.Trim() ?? "";
                var vram = gpu.CimInstanceProperties["AdapterRAM"]?.Value;
                if (vram is uint v && v > 0) info.GpuVram = $"{v / 1073741824.0:F1} GB";
                info.GpuDriver = gpu.CimInstanceProperties["DriverVersion"]?.Value?.ToString() ?? "";
            }
        }
        catch { }

        // ── Disks ──
        try
        {
            foreach (var disk in Query("SELECT DeviceID,VolumeName,Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3"))
            {
                info.Disks.Add(new DiskInfo
                {
                    Id = disk.CimInstanceProperties["DeviceID"]?.Value?.ToString() ?? "",
                    Label = disk.CimInstanceProperties["VolumeName"]?.Value?.ToString() ?? "",
                    Total = Convert.ToUInt64(disk.CimInstanceProperties["Size"]?.Value ?? 0),
                    Free = Convert.ToUInt64(disk.CimInstanceProperties["FreeSpace"]?.Value ?? 0)
                });
            }
        }
        catch { }

        // ── Battery ──
        try
        {
            var bat = Query("SELECT EstimatedChargeRemaining,BatteryStatus FROM Win32_Battery").FirstOrDefault();
            if (bat != null)
            {
                var charge = bat.CimInstanceProperties["EstimatedChargeRemaining"]?.Value;
                if (charge is ushort pct) info.BatteryPercent = $"{pct}%";
                var status = bat.CimInstanceProperties["BatteryStatus"]?.Value;
                info.BatteryStatus = status switch
                {
                    1 => "Descargando", 2 => "Enchufado", 3 => "Cargando",
                    4 => "Cargado", _ => $"Estado {status}"
                };
            }
        }
        catch { }

        // ── Motherboard / BIOS ──
        try
        {
            var mb = Query("SELECT Manufacturer,Product FROM Win32_BaseBoard").FirstOrDefault();
            if (mb != null)
                info.Motherboard = $"{mb.CimInstanceProperties["Manufacturer"]?.Value} {mb.CimInstanceProperties["Product"]?.Value}";

            var bios = Query("SELECT SMBIOSBIOSVersion,ReleaseDate FROM Win32_BIOS").FirstOrDefault();
            if (bios != null)
                info.Bios = bios.CimInstanceProperties["SMBIOSBIOSVersion"]?.Value?.ToString() ?? "";
        }
        catch { }

        // ── Uptime ──
        try
        {
            var os = Query("SELECT LastBootUpTime FROM Win32_OperatingSystem").FirstOrDefault();
            if (os?.CimInstanceProperties["LastBootUpTime"]?.Value is DateTime boot)
            {
                var span = DateTime.Now - boot;
                if (span.TotalDays >= 1)
                    info.Uptime = $"{(int)span.TotalDays} días, {span.Hours} horas";
                else
                    info.Uptime = $"{span.Hours} horas, {span.Minutes} min";
            }
        }
        catch { }

        return info;
    }

    private static string? _cachedCpuName;

    // Nombre del procesador (para el dashboard). Consulta ligera + cache:
    // no re-ejecuta la query CIM completa de Collect() en cada visita.
    public static string GetCpuName()
    {
        if (_cachedCpuName != null) return _cachedCpuName;
        try
        {
            var cpu = Query("SELECT Name FROM Win32_Processor").FirstOrDefault();
            _cachedCpuName = cpu?.CimInstanceProperties["Name"]?.Value?.ToString()?.Trim() ?? "No detectado";
        }
        catch
        {
            _cachedCpuName = "No detectado";
        }
        return _cachedCpuName;
    }

    public static string FormatReport(SysInfo i)
    {
        var lines = new[]
        {
            $"=== Informe de Wachin — {DateTime.Now:g} ===",
            "",
            $"Sistema: {i.OsName} {i.OsVersion} (Build {i.OsBuild}) {i.OsArch}",
            $"Equipo: {i.MachineName}",
            "",
            $"Procesador: {i.CpuName}",
            $"  Núcleos: {i.CpuCores} | Hilos: {i.CpuThreads} | Frecuencia: {i.CpuSpeed}",
            "",
            $"Memoria RAM: {DiskInfo.FormatBytes(i.RamUsed)} / {DiskInfo.FormatBytes(i.RamTotal)} ({i.RamPercent}%)",
            "",
            $"GPU: {i.GpuName}",
            $"  VRAM: {i.GpuVram} | Driver: {i.GpuDriver}",
            ""
        };

        if (i.Disks.Any())
        {
            lines = lines.Append("Discos:").ToArray();
            foreach (var d in i.Disks)
                lines = lines.Append($"  {d.Id} {d.Label} — libre: {d.FreeLabel} de {d.TotalLabel}").ToArray();
            lines = lines.Append("").ToArray();
        }

        if (!string.IsNullOrEmpty(i.BatteryPercent))
            lines = lines.Append($"Batería: {i.BatteryPercent} ({i.BatteryStatus})").Append("").ToArray();

        if (!string.IsNullOrEmpty(i.Motherboard))
            lines = lines.Append($"Placa: {i.Motherboard}").ToArray();
        if (!string.IsNullOrEmpty(i.Bios))
            lines = lines.Append($"BIOS: {i.Bios}").ToArray();
        if (!string.IsNullOrEmpty(i.Uptime))
            lines = lines.Append($"Tiempo activo: {i.Uptime}").ToArray();

        return string.Join(Environment.NewLine, lines);
    }

    // ── Live HUD status (fast, no CIM queries) ──────────────────────────────
    public sealed class QuickStatus
    {
        public int RamPercent { get; set; }
        public string RamUsed { get; set; } = "";
        public string RamTotal { get; set; } = "";
        public string Uptime { get; set; } = "";
        public string Clock { get; set; } = "";
        public string Date { get; set; } = "";
    }

    public static QuickStatus GetQuickStatus()
    {
        var s = new QuickStatus
        {
            Clock = DateTime.Now.ToString("HH:mm:ss"),
            Date = DateTime.Now.ToString("dd/MM/yyyy")
        };

        try
        {
            var mem = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ref mem))
            {
                s.RamPercent = (int)mem.dwMemoryLoad;
                s.RamUsed = DiskInfo.FormatBytes(mem.ullTotalPhys - mem.ullAvailPhys);
                s.RamTotal = DiskInfo.FormatBytes(mem.ullTotalPhys);
            }
        }
        catch { }

        var span = TimeSpan.FromMilliseconds(Environment.TickCount64);
        if (span.TotalDays >= 1)
            s.Uptime = $"{(int)span.TotalDays}d {span.Hours}h";
        else if (span.TotalHours >= 1)
            s.Uptime = $"{span.Hours}h {span.Minutes}m";
        else
            s.Uptime = $"{span.Minutes}m";

        return s;
    }

    // ── P/Invoke for memory status ──
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>(); }
    }
}
