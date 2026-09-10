namespace MRS.DismEngine.Processes;

/// <summary>
/// Resultado de ejecutar un proceso externo. El <see cref="ExitCode"/> es el
/// indicador principal de éxito; el texto "ERROR" en la salida NO se usa como
/// criterio de fallo.
/// </summary>
public sealed record ProcessRunResult(
    string FileName,
    string Arguments,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut)
{
    public bool Succeeded => !TimedOut && ExitCode == 0;

    public string CommandLine =>
        string.IsNullOrEmpty(Arguments) ? FileName : $"{FileName} {Arguments}";
}
