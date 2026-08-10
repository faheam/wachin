using System.IO;
using System.Runtime.InteropServices;
using Wachin.Models;

namespace Wachin.Services;

public static class CleanerService
{
    public static List<CleanerCategory> BuildCategories() => new()
    {
        new()
        {
            Id = "temp-user",
            Name = "Archivos temporales de usuario",
            Description = "Archivos creados por apps que ya no se necesitan (%TEMP%).",
            Risk = RiskLevel.Low,
            Paths = { Path.GetTempPath() }
        },
        new()
        {
            Id = "temp-win",
            Name = "Archivos temporales de Windows",
            Description = "Archivos temporales del sistema operativo (C:\\Windows\\Temp).",
            Risk = RiskLevel.Medium,
            Paths = { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") }
        },
        new()
        {
            Id = "wu-cache",
            Name = "Caché de Windows Update",
            Description = "Descargas de actualizaciones acumuladas. Se vuelven a descargar si se necesitan.",
            Risk = RiskLevel.Low,
            Paths = { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\Download") }
        },
        new()
        {
            Id = "prefetch",
            Name = "Prefetch",
            Description = "Archivos de aceleración de inicio de apps. Se regeneran, pero el primer inicio puede ser un poco más lento.",
            Risk = RiskLevel.Low,
            Paths = { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch") }
        },
        new()
        {
            Id = "thumbcache",
            Name = "Caché de miniaturas",
            Description = "Imágenes en miniatura de fotos y archivos. Se regeneran automáticamente.",
            Risk = RiskLevel.Low,
            Paths = { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Explorer") }
        },
        new()
        {
            Id = "d3d",
            Name = "Caché de shaders DirectX",
            Description = "Archivos temporales de renderizado gráfico. Seguros de eliminar.",
            Risk = RiskLevel.Low,
            Paths = { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"D3DSCache") }
        },
        new()
        {
            Id = "wer",
            Name = "Informes de errores",
            Description = "Reportes de errores de apps y del sistema. No afectan el funcionamiento.",
            Risk = RiskLevel.Low,
            Paths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\WER\ReportQueue"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\WER\ReportArchive"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\WER\ReportQueue"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\WER\ReportArchive")
            }
        },
        new()
        {
            Id = "delivery-opt",
            Name = "Caché de optimización de entregas",
            Description = "Archivos de entrega de actualizaciones Windows Update. Seguros de eliminar.",
            Risk = RiskLevel.Low,
            Paths = { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\DeliveryOptimization") }
        },
        new()
        {
            Id = "recycle",
            Name = "Papelera de reciclaje",
            Description = "Archivos que borraste pero no eliminaste permanentemente.",
            Risk = RiskLevel.Low,
            SupportsSize = false // handled specially
        }
    };

    public static async Task<(long SizeBytes, int ItemCount)> ScanAsync(CleanerCategory cat, IProgress<string>? progress = null)
    {
        long totalSize = 0;
        int count = 0;

        if (cat.Id == "recycle")
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            SHQueryRecycleBinW(null, ref info);
            return (info.i64Size, (int)info.i64NumItems);
        }

        foreach (string dir in cat.Paths)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var opts = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.None,
                    ReturnSpecialDirectories = false
                };

                await Task.Run(() =>
                {
                    foreach (string file in Directory.EnumerateFiles(dir, "*", opts))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            totalSize += fi.Length;
                            count++;
                        }
                        catch { }
                    }
                });
            }
            catch { }
        }

        return (totalSize, count);
    }

    public static async Task<(long BytesFreed, int Items, int Skipped)> CleanAsync(
        CleanerCategory cat, IProgress<string>? progress = null)
    {
        long freed = 0;
        int items = 0;
        int skipped = 0;

        if (cat.Id == "recycle")
        {
            int hr = SHEmptyRecycleBinW(IntPtr.Zero, null, 7); // SHERB_NOCONFIRMATION|SHERB_NOPROGRESSUI|SHERB_NOSOUND
            return (0, 0, hr == 0 ? 0 : 1);
        }

        foreach (string dir in cat.Paths)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var opts = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.None,
                    ReturnSpecialDirectories = false
                };

                await Task.Run(() =>
                {
                    foreach (string file in Directory.EnumerateFiles(dir, "*", opts))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            long len = fi.Length;
                            fi.Delete();
                            freed += len;
                            items++;
                        }
                        catch
                        {
                            skipped++;
                        }
                    }
                });

                // Try to remove empty subdirectories (best effort)
                try
                {
                    foreach (string sub in Directory.EnumerateDirectories(dir))
                    {
                        try
                        {
                            if (!Directory.EnumerateFileSystemEntries(sub).Any())
                                Directory.Delete(sub, false);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            catch { skipped++; }
        }

        progress?.Report($"Limpiados {FormatSize(freed)} en {items} archivos ({skipped} saltados).");
        return (freed, items, skipped);
    }

    private static string FormatSize(long b) => b switch
    {
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{b / 1048576.0:F1} MB",
        _ => $"{b / 1073741824.0:F2} GB"
    };

    // ── P/Invoke: Recycle Bin ──

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBinW(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBinW(IntPtr hwnd, string? pszRootPath, uint dwFlags);
}
