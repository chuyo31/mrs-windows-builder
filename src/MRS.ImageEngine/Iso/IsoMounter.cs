using MRS.DismEngine.Logging;
using MRS.DismEngine.Processes;

namespace MRS.ImageEngine.Iso;

/// <summary>
/// Montaje de ISO mediante los cmdlets de Windows (<c>Mount-DiskImage</c> /
/// <c>Dismount-DiskImage</c>) a través de PowerShell. La imagen se monta como
/// unidad de solo lectura; ningún archivo de la ISO se modifica. Siempre se
/// desmonta al liberar el objeto devuelto.
/// </summary>
public sealed class IsoMounter : IIsoMounter
{
    private static readonly TimeSpan MountTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DismountTimeout = TimeSpan.FromMinutes(1);

    private readonly IProcessRunner _processRunner;
    private readonly IAppLogger _logger;

    public IsoMounter(IProcessRunner processRunner, IAppLogger? logger = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task<IIsoMount> MountAsync(string isoPath, CancellationToken cancellationToken = default)
    {
        var escaped = isoPath.Replace("'", "''");
        var script =
            "$ErrorActionPreference='Stop';" +
            $"Mount-DiskImage -ImagePath '{escaped}' -Access ReadOnly | Out-Null;" +
            "Start-Sleep -Milliseconds 400;" +
            $"$vol = Get-DiskImage -ImagePath '{escaped}' | Get-Volume;" +
            "if (-not $vol -or -not $vol.DriveLetter) { throw 'La ISO no obtuvo letra de unidad.' };" +
            "Write-Output $vol.DriveLetter";

        _logger.Info($"Montando ISO: {isoPath}");

        var result = await _processRunner
            .RunAsync("powershell.exe", $"-NoProfile -NonInteractive -Command \"{script}\"", cancellationToken, MountTimeout)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            _logger.Error($"No se pudo montar la ISO (código {result.ExitCode}).");
            throw new ImageAnalysisException(result.ExitCode, "No se pudo montar la ISO.");
        }

        var driveLetter = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()?.Trim();

        if (string.IsNullOrWhiteSpace(driveLetter))
        {
            await BestEffortDismountAsync(isoPath).ConfigureAwait(false);
            throw new ImageAnalysisException(-1, "No se pudo determinar la unidad de la ISO montada.");
        }

        var root = $"{driveLetter}:\\";
        _logger.Info($"ISO montada en {root}");
        return new IsoMount(isoPath, root, this);
    }

    private async Task BestEffortDismountAsync(string isoPath)
    {
        var escaped = isoPath.Replace("'", "''");
        var script =
            "$ErrorActionPreference='SilentlyContinue';" +
            $"Dismount-DiskImage -ImagePath '{escaped}' | Out-Null";

        var result = await _processRunner
            .RunAsync("powershell.exe", $"-NoProfile -NonInteractive -Command \"{script}\"", CancellationToken.None, DismountTimeout)
            .ConfigureAwait(false);

        _logger.Info(result.Succeeded
            ? "ISO desmontada."
            : $"Aviso: la ISO puede no haberse desmontado limpiamente (código {result.ExitCode}).");
    }

    private sealed class IsoMount : IIsoMount
    {
        private readonly string _isoPath;
        private readonly IsoMounter _owner;
        private int _disposed;

        public IsoMount(string isoPath, string rootPath, IsoMounter owner)
        {
            _isoPath = isoPath;
            RootPath = rootPath;
            _owner = owner;
        }

        public string RootPath { get; }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            await _owner.BestEffortDismountAsync(_isoPath).ConfigureAwait(false);
        }
    }
}
