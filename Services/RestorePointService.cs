using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Wachin.Services;

public static class RestorePointService
{
    private const int BEGIN_SYSTEM_CHANGE = 100;
    private const int END_SYSTEM_CHANGE = 101;
    private const int MODIFY_SETTINGS = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RESTOREPOINTINFO
    {
        public int dwEventType;
        public int dwRestorePtType;
        public long llSequenceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STATEMGRSTATUS
    {
        public int nStatusCode;
        public long llSequenceNumber;
    }

    [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SRSetRestorePoint(ref RESTOREPOINTINFO pRestorePtInfo, out STATEMGRSTATUS pStatus);

    public static bool IsProtectionEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", false);
            if (key == null) return false;

            var disable = key.GetValue("DisableSR");
            if (disable is int d && d == 1) return false;

            var global = key.GetValue("RPGloballyDisabled");
            if (global is int g && g == 1) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static (bool Ok, string Message) Create(string description)
    {
        if (!IsProtectionEnabled())
            return (false, "La protección del sistema está desactivada. Actívala desde Configuración del sistema → Protección del sistema.");

        try
        {
            var info = new RESTOREPOINTINFO
            {
                dwEventType = BEGIN_SYSTEM_CHANGE,
                dwRestorePtType = MODIFY_SETTINGS,
                szDescription = description
            };
            int r1 = SRSetRestorePoint(ref info, out var status);

            info.dwEventType = END_SYSTEM_CHANGE;
            int r2 = SRSetRestorePoint(ref info, out _);

            if (r1 != 0 && r2 != 0 && status.nStatusCode == 0)
                return (true, "Punto de restauración creado correctamente.");

            return (false, status.nStatusCode switch
            {
                1058 => "El servicio de restauración del sistema no está activo. Revisá la configuración del sistema.",
                _ => $"No se pudo crear el punto de restauración (código: {status.nStatusCode})."
            });
        }
        catch (Exception ex)
        {
            return (false, $"Error inesperado: {ex.Message}");
        }
    }

    public static void OpenSystemProtectionSettings()
    {
        System.Diagnostics.Process.Start("sysdm.cpl", "/4");
    }
}
