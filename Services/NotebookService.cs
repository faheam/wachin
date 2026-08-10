using System.Text.RegularExpressions;

namespace Wachin.Services;

// Seccion Notebook: controla el estado maximo del procesador para bajar la
// temperatura en notebooks (equivalente a los modos 99%/100% del script).
public static class NotebookService
{
    private const string Cmd = "powercfg.exe";

    /// <summary>Aplica el limite maximo de CPU: 99 (gaming/frio) o 100 (normal).</summary>
    public static async Task<(bool Ok, string Msg)> SetThrottleMaxAsync(int percent)
    {
        if (percent != 99 && percent != 100)
            return (false, "Valor no válido. Solo se admite 99 o 100.");

        var (c1, _, e1) = await ProcessRunner.RunAsync(Cmd,
            $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {percent}");
        var (c2, _, e2) = await ProcessRunner.RunAsync(Cmd,
            $"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {percent}");
        var (c3, _, e3) = await ProcessRunner.RunAsync(Cmd, "/setactive SCHEME_CURRENT");

        if (c1 == 0 && c2 == 0 && c3 == 0)
        {
            return (true, percent == 99
                ? "CPU limitada al 99%. Tu notebook va a estar mucho más fresca."
                : "CPU restaurada al 100%. Rendimiento completo de nuevo.");
        }

        var err = string.Join(" | ", new[] { e1, e2, e3 }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return (false, string.IsNullOrWhiteSpace(err) ? "Error desconocido de powercfg." : err);
    }

    /// <summary>Lee el valor AC actual de PROCTHROTTLEMAX (99 o 100). Devuelve -1 si no puede.</summary>
    /// <remarks>
    /// Usamos /q en vez de /getacvalueindex: este ultimo falla con "Parámetros no
    /// válidos" en varios sistemas (ni con aliases ni con GUIDs), mientras que /q
    /// siempre devuelve el valor. Las lineas de valor tienen un formato de hex
    /// estable entre idiomas (solo cambian los labels traducidos).
    /// </remarks>
    public static int GetCurrentThrottleMax()
    {
        var (code, stdout, _) = ProcessRunner.Run(Cmd,
            "/q SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX", 10_000);
        if (code != 0) return -1;

        // El output termina con los valores AC y DC actuales (0x00000063 = 99,
        // 0x00000064 = 100). Tomamos el penultimo, que es el de corriente alterna.
        var hexes = Regex.Matches(stdout, @"0x[0-9A-Fa-f]{6,8}")
                         .Select(m => m.Value)
                         .ToList();
        if (hexes.Count >= 2)
        {
            try { return Convert.ToInt32(hexes[^2], 16); }
            catch { /* hex mal formado */ }
        }
        return -1;
    }
}
