using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace MRS.DismEngine.Processes;

/// <summary>
/// Ejecutor robusto de procesos externos: captura stdout/stderr de forma
/// asíncrona, el código de salida y la duración, y admite un tiempo máximo.
/// Nunca lanza por un código de salida distinto de cero: devuelve el resultado.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"No se pudo iniciar el proceso '{fileName}'.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            stopwatch.Stop();
            return new ProcessRunResult(fileName, arguments, -1, string.Empty, ex.Message, stopwatch.Elapsed, false);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is { } t)
            linked.CancelAfter(t);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = !cancellationToken.IsCancellationRequested;
            TryKill(process);

            if (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                throw;
            }
        }

        // Deja que se vacíen los búferes asíncronos de salida.
        try { process.WaitForExit(); } catch { /* ya finalizado */ }
        stopwatch.Stop();

        var exitCode = timedOut ? -1 : SafeExitCode(process);
        return new ProcessRunResult(
            fileName, arguments, exitCode,
            stdout.ToString(), stderr.ToString(), stopwatch.Elapsed, timedOut);
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); } catch { /* nada que hacer */ }
        try { process.WaitForExit(2000); } catch { /* nada que hacer */ }
    }

    private static int SafeExitCode(Process process)
    {
        try { return process.ExitCode; } catch { return -1; }
    }
}
