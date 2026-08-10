using System.Diagnostics;
using System.Text;

namespace Wachin.Services;

public static class ProcessRunner
{
    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string fileName, string arguments, int timeoutMs = 30_000)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeoutMs);
            try { await process.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { /* best effort */ }
                return (-1, "", "timeout");
            }

            string stdout = await outTask;
            string stderr = await errTask;
            return (process.ExitCode, stdout.Trim(), stderr.Trim());
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }

    public static (int ExitCode, string StdOut, string StdErr) Run(
        string fileName, string arguments, int timeoutMs = 30_000)
    {
        return RunAsync(fileName, arguments, timeoutMs).GetAwaiter().GetResult();
    }

    public static string RunPowerShell(string command)
    {
        var (code, stdout, _) = Run("powershell.exe",
            $"-NoProfile -NonInteractive -Command \"{command}\"", 15_000);
        return code == 0 ? stdout : "";
    }
}
