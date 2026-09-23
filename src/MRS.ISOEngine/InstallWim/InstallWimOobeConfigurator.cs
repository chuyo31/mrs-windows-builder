using MRS.DismEngine.Dism;
using MRS.DismEngine.Logging;
using MRS.ISOEngine.Configuration;
using MRS.ISOEngine.Exceptions;
using MRS.ISOEngine.Models;
using MRS.ISOEngine.Registry;

namespace MRS.ISOEngine.InstallWim;

/// <summary>Implementación de <see cref="IInstallWimOobeConfigurator"/>. Ver la interfaz para el ciclo completo.</summary>
public sealed class InstallWimOobeConfigurator : IInstallWimOobeConfigurator
{
    // Mismo umbral de cordura que BootWimModifier (P25): no un tamaño real de
    // install.wim (que ronda GB), solo para descartar un archivo vacío/truncado.
    private const long MinimumPlausibleInstallWimSizeBytes = 4096;

    private readonly IDismRunner _dism;
    private readonly IOfflineRegistryEditor _registry;
    private readonly LabConfigApplier _labConfigApplier;
    private readonly IAppLogger _logger;

    public InstallWimOobeConfigurator(IDismRunner dismRunner, IOfflineRegistryEditor registry, IAppLogger? logger = null)
    {
        _dism = dismRunner ?? throw new ArgumentNullException(nameof(dismRunner));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? NullAppLogger.Instance;
        // Reutiliza el mismo mecanismo ya usado para boot.wim (P16/P28): LabConfigApplier
        // no sabe ni le importa de qué WIM viene el hive que recibe cargado.
        _labConfigApplier = new LabConfigApplier(_registry, _logger);
    }

    public async Task<InstallWimOobeConfigurationResult> ApplyOfflineOobeBypassAsync(
        string installWimPath, int index, string mountDir,
        CancellationToken cancellationToken = default, IProgress<InstallationProgressInfo>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installWimPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountDir);

        const string mountingStage = "Configurando OOBE offline en install.wim";
        const string validatingStage = "Verificando OOBE offline en install.wim";

        await ValidateBeforeMountAsync(installWimPath, cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(mountDir);

        _logger.Info("Montando la imagen de trabajo de install.wim (ReadWrite) para configurar OOBE offline...");
        progress?.Report(InstallationProgressInfo.Create(mountingStage, 80, "DISM: Mount-Wim de install.wim iniciado"));
        var mountResult = await _dism.MountWimAsync(installWimPath, index, mountDir, readOnly: false, cancellationToken)
            .ConfigureAwait(false);

        if (!mountResult.Succeeded)
        {
            progress?.Report(InstallationProgressInfo.Create(
                mountingStage, 80, $"DISM: Mount-Wim falló (ExitCode {mountResult.ExitCode})", InstallationProgressLevel.Error));
            throw new IsoEngineException(
                $"No se pudo montar install.wim para configurar OOBE offline (ExitCode {mountResult.ExitCode}).", mountResult.ExitCode);
        }

        var hiveKeyName = "MRS_ISOENGINE_INSTALLWIM_SYSTEM_" + Guid.NewGuid().ToString("N")[..8];
        var hiveFilePath = Path.Combine(mountDir, "Windows", "System32", "config", "SYSTEM");

        var applied = new List<string>();
        var errors = new List<string>();
        var hiveLoaded = false;
        var success = false;

        try
        {
            var loadResult = await _registry.LoadHiveAsync(hiveKeyName, hiveFilePath, cancellationToken).ConfigureAwait(false);
            if (!loadResult.Succeeded)
                throw new IsoEngineException(
                    $"No se pudo cargar el hive SYSTEM offline de install.wim (ExitCode {loadResult.ExitCode}).", loadResult.ExitCode);

            hiveLoaded = true;
            _logger.Info("[OK] install.wim OOBE hive loaded");

            var oobeApplied = await _labConfigApplier.ApplyOfflineOobeBypassAsync(hiveKeyName, cancellationToken).ConfigureAwait(false);
            if (!oobeApplied)
                throw new IsoEngineException("No se pudo aplicar BypassNRO en install.wim.");

            applied.Add("[COMPAT] BypassNRO applied to install.wim");
            _logger.Info("[OK] BypassNRO = 1");

            progress?.Report(InstallationProgressInfo.Create(validatingStage, 81, "Verificando BypassNRO en install.wim..."));
            var verification = await _labConfigApplier.VerifyOfflineOobeBypassAsync(hiveKeyName, cancellationToken).ConfigureAwait(false);
            if (!verification.IsValid)
            {
                errors.AddRange(verification.Errors);
            }
            else
            {
                _logger.Info("[OK] install.wim OOBE configuration verified");
            }

            success = verification.IsValid;
        }
        catch (IsoEngineException ex)
        {
            errors.Add(ex.Message);
            success = false;
        }
        finally
        {
            // Garantizar limpieza incluso ante excepciones: el hive siempre se
            // intenta descargar si se llegó a cargar, pase lo que pase arriba.
            if (hiveLoaded)
            {
                var unloadResult = await _registry.UnloadHiveAsync(hiveKeyName, cancellationToken).ConfigureAwait(false);
                if (!unloadResult.Succeeded)
                {
                    errors.Add($"No se pudo descargar el hive de registro offline de install.wim (ExitCode {unloadResult.ExitCode}).");
                    success = false;
                }
            }
        }

        if (success)
        {
            progress?.Report(InstallationProgressInfo.Create(validatingStage, 81, "Confirmando cambios en install.wim..."));
            var commit = await _dism.UnmountWimCommitAsync(mountDir, cancellationToken).ConfigureAwait(false);
            if (!commit.Succeeded)
            {
                errors.Add($"No se pudo confirmar install.wim (ExitCode {commit.ExitCode}).");
                success = false;
            }
            else
            {
                progress?.Report(InstallationProgressInfo.Create(validatingStage, 82, "install.wim confirmado", InstallationProgressLevel.Success));
            }
        }
        else
        {
            _logger.Warn("Descartando cambios de OOBE offline en install.wim por un fallo previo.");
            progress?.Report(InstallationProgressInfo.Create(
                validatingStage, 81, "Descartando cambios en install.wim...", InstallationProgressLevel.Warning));
            await _dism.UnmountWimDiscardAsync(mountDir, cancellationToken).ConfigureAwait(false);
        }

        return new InstallWimOobeConfigurationResult(success, applied, errors);
    }

    /// <summary>Mismo patrón defensivo que <c>BootWimModifier.ValidateBeforeMountAsync</c> (P25/P26).</summary>
    private async Task ValidateBeforeMountAsync(string installWimPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(installWimPath))
            throw new IsoEngineException($"No se encuentra install.wim: '{installWimPath}'.");

        var info = new FileInfo(installWimPath);
        if (info.Length < MinimumPlausibleInstallWimSizeBytes)
            throw new IsoEngineException(
                $"install.wim es sospechosamente pequeño ({info.Length} bytes); no se intentará montar.");

        if ((info.Attributes & FileAttributes.ReadOnly) != 0)
            throw new IsoEngineException(
                $"install.wim todavía tiene el atributo ReadOnly: '{installWimPath}'. No se intentará montar.");

        var wimInfo = await _dism.GetWimInfoAsync(installWimPath, cancellationToken).ConfigureAwait(false);
        if (!wimInfo.Succeeded)
            throw new IsoEngineException(
                $"install.wim no es un WIM válido (DISM /Get-WimInfo ExitCode {wimInfo.ExitCode}).", wimInfo.ExitCode);
    }
}
