using Microsoft.Win32;

namespace Wachin.Services;

public static class RegistryOps
{
    public static RegistryKey? OpenHive(string hive, bool writable)
    {
        return hive.ToUpperInvariant() switch
        {
            "HKCU" => Registry.CurrentUser,
            "HKLM" => Registry.LocalMachine,
            "HKLM32" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32),
            "HKLM64" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
            _ => null
        };
    }

    /// <summary>Read a registry value. Returns (value, kind, exists).</summary>
    public static (object? Value, RegistryValueKind Kind, bool Exists) Read(string hive, string path, string name)
    {
        using var root = OpenHive(hive, false);
        if (root == null) return (null, RegistryValueKind.Unknown, false);

        using var key = root.OpenSubKey(path, false);
        if (key == null) return (null, RegistryValueKind.Unknown, false);

        try
        {
            object? val = key.GetValue(name);
            if (val == null && !key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase))
                return (null, RegistryValueKind.Unknown, false);

            var kind = key.GetValueKind(name);
            return (val, kind, true);
        }
        catch
        {
            return (null, RegistryValueKind.Unknown, false);
        }
    }

    /// <summary>Write a value. Creates the subkey if needed.</summary>
    public static void Write(string hive, string path, string name, object? value, RegistryValueKind kind)
    {
        using var root = OpenHive(hive, true) ?? throw new Exception($"No se pudo abrir la rama {hive}");
        using var key = root.CreateSubKey(path) ?? throw new Exception($"No se pudo crear la clave {hive}\\{path}");

        if (value == null)
        {
            key.DeleteValue(name, false);
        }
        else
        {
            key.SetValue(name, value, kind);
        }
    }

    /// <summary>Delete a single value from a key.</summary>
    public static void DeleteValue(string hive, string path, string name)
    {
        using var root = OpenHive(hive, true) ?? throw new Exception($"No se pudo abrir {hive}");
        using var key = root.OpenSubKey(path, true);
        key?.DeleteValue(name, false);
    }

    /// <summary>Delete an entire key (recursively).</summary>
    public static void DeleteKey(string hive, string path)
    {
        using var root = OpenHive(hive, true) ?? throw new Exception($"No se pudo abrir {hive}");
        root.DeleteSubKeyTree(path, false);
    }

    /// <summary>Read the DWORD value at a path, or the default if not found.</summary>
    public static int ReadInt(string hive, string path, string name, int defaultValue = 0)
    {
        var (val, _, exists) = Read(hive, path, name);
        if (!exists) return defaultValue;
        if (val is int i) return i;
        if (val is uint u) return (int)u;
        if (val is string s && int.TryParse(s, out int parsed)) return parsed;
        return defaultValue;
    }

    /// <summary>Write a DWORD value.</summary>
    public static void WriteInt(string hive, string path, string name, int value)
    {
        Write(hive, path, name, value, RegistryValueKind.DWord);
    }

    /// <summary>Write a REG_SZ string value.</summary>
    public static void WriteString(string hive, string path, string name, string value)
    {
        Write(hive, path, name, value, RegistryValueKind.String);
    }

    /// <summary>Read all values in a key. Returns list of (name, value).</summary>
    public static List<(string Name, object? Value, RegistryValueKind Kind)> ReadValues(string hive, string path)
    {
        var result = new List<(string, object?, RegistryValueKind)>();
        using var root = OpenHive(hive, false);
        if (root == null) return result;

        using var key = root.OpenSubKey(path, false);
        if (key == null) return result;

        foreach (var name in key.GetValueNames())
        {
            try
            {
                result.Add((name, key.GetValue(name), key.GetValueKind(name)));
            }
            catch { /* skip inaccessible values */ }
        }
        return result;
    }

    /// <summary>Check if a key exists.</summary>
    public static bool KeyExists(string hive, string path)
    {
        using var root = OpenHive(hive, false);
        if (root == null) return false;
        using var key = root.OpenSubKey(path, false);
        return key != null;
    }
}
