using MRS.DismEngine.Logging;
using MRS.DismEngine.Processes;

namespace MRS.DismEngine.Dism;

/// <summary>
/// Implementación de <see cref="IDismRunner"/> sobre DISM.exe.
///
/// Cadena de responsabilidad:
///   ImageService  ->  DismRunner  ->  IProcessRunner  ->  DISM.exe
///
/// Se fuerza <c>/English</c> para que la salida sea estable independientemente
/// del idioma de Windows. El comando ejecutado se registra en el log con el
/// nivel [DISM]; el usuario final no ve estos detalles técnicos.
/// </summary>
public sealed class DismRunner : IDismRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    private readonly IProcessRunner _processRunner;
    private readonly IAppLogger _logger;
    private readonly string _dismPath;

    public DismRunner(IProcessRunner processRunner, IAppLogger? logger = null, string? dismPath = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? NullAppLogger.Instance;
        _dismPath = dismPath ?? ResolveDismPath();
    }

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Get-WimInfo /WimFile:\"{imagePath}\"", cancellationToken);

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, int index, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Get-WimInfo /WimFile:\"{imagePath}\" /Index:{index}", cancellationToken);

    private async Task<ProcessRunResult> ExecuteAsync(string arguments, CancellationToken cancellationToken)
    {
        _logger.Dism($"{_dismPath} {arguments}");

        var result = await _processRunner
            .RunAsync(_dismPath, arguments, cancellationToken, DefaultTimeout)
            .ConfigureAwait(false);

        _logger.Dism($"ExitCode={result.ExitCode} Duracion={result.Duration.TotalSeconds:0.0}s");

        if (result.TimedOut)
            _logger.Warn("DISM no respondió dentro del tiempo máximo.");
        else if (result.ExitCode != 0)
            _logger.Warn($"DISM devolvió un código distinto de cero: {result.ExitCode}");

        return result;
    }

    private static string ResolveDismPath()
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var candidate = Path.Combine(system, "Dism.exe");
        return File.Exists(candidate) ? candidate : "Dism.exe";
    }
}
