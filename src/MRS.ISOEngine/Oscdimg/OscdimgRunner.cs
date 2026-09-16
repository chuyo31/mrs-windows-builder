using MRS.DismEngine.Processes;

namespace MRS.ISOEngine.Oscdimg;

/// <summary>
/// Implementación de <see cref="IOscdimgRunner"/> sobre <c>oscdimg.exe</c>
/// (Windows ADK — Deployment Tools). No forma parte de Windows: si no está
/// instalado, <see cref="IsAvailable"/> lo dice explícitamente en vez de
/// dejar que el primer intento de generar la ISO falle de forma confusa —
/// mismo principio que P17 con la comprobación de privilegios elevados antes
/// de intentar nada real.
/// </summary>
public sealed class OscdimgRunner : IOscdimgRunner
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(30);

    private static readonly string[] CommonInstallPaths =
    {
        @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
        @"C:\Program Files\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
    };

    private readonly IProcessRunner _processRunner;
    private readonly string? _oscdimgPath;

    public OscdimgRunner(IProcessRunner processRunner, string? oscdimgPath = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        // Se resuelve una única vez en el constructor, pero SIEMPRE se
        // revalida su existencia real con File.Exists en cada llamada
        // (IsAvailable/BuildIsoAsync) — nunca se confía ciegamente en una ruta
        // explícita, propia o ajena.
        _oscdimgPath = oscdimgPath ?? ResolveOscdimgPath();
    }

    public bool IsAvailable() => _oscdimgPath is not null && File.Exists(_oscdimgPath);

    public Task<ProcessRunResult> BuildIsoAsync(string workspaceRoot, string outputIsoPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputIsoPath);

        if (!IsAvailable())
            throw new Exceptions.IsoEngineException(
                "oscdimg.exe no está disponible en este equipo (Windows ADK — Deployment Tools no instalado). " +
                "No se puede generar la ISO final.");

        var etfsboot = Path.Combine(workspaceRoot, "boot", "etfsboot.com");
        var efisys = Path.Combine(workspaceRoot, "efi", "microsoft", "boot", "efisys.bin");

        var arguments =
            "-m -o -u2 -udfver102 " +
            $"-bootdata:2#p0,e,b\"{etfsboot}\"#pEF,e,b\"{efisys}\" " +
            $"\"{workspaceRoot}\" \"{outputIsoPath}\"";

        // IsAvailable() ya ha confirmado arriba que _oscdimgPath no es null.
        return _processRunner.RunAsync(_oscdimgPath!, arguments, cancellationToken, BuildTimeout);
    }

    private static string? ResolveOscdimgPath()
    {
        foreach (var candidate in CommonInstallPaths)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // También podría estar en PATH; se resuelve en tiempo de ejecución del
        // propio proceso (igual que "Dism.exe"/"reg.exe" en los otros runners),
        // pero solo si de verdad existe: nunca se asume "oscdimg.exe" a ciegas
        // porque esta herramienta no viene con Windows.
        return null;
    }
}
