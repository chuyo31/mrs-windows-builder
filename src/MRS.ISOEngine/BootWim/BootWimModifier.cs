using MRS.DismEngine.Dism;
using MRS.DismEngine.Logging;
using MRS.ISOEngine.Configuration;
using MRS.ISOEngine.Exceptions;
using MRS.ISOEngine.Models;
using MRS.ISOEngine.Registry;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.BootWim;

/// <summary>Implementación de <see cref="IBootWimModifier"/>. Ver la interfaz para el ciclo completo.</summary>
public sealed class BootWimModifier : IBootWimModifier
{
    // P25: umbral de cordura, no un tamaño real de boot.wim (que ronda cientos de
    // MB) -- solo para descartar un archivo vacío/truncado antes de intentar
    // montarlo, sin exigir un tamaño "de verdad" que un WIM legítimo más pequeño
    // pudiera no alcanzar.
    private const long MinimumPlausibleBootWimSizeBytes = 4096;

    private readonly IDismRunner _dism;
    private readonly IOfflineRegistryEditor _registry;
    private readonly LabConfigApplier _labConfigApplier;
    private readonly IAppLogger _logger;

    public BootWimModifier(IDismRunner dismRunner, IOfflineRegistryEditor registry, IAppLogger? logger = null)
    {
        _dism = dismRunner ?? throw new ArgumentNullException(nameof(dismRunner));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? NullAppLogger.Instance;
        _labConfigApplier = new LabConfigApplier(_registry, _logger);
    }

    public async Task<BootWimModificationResult> ApplyLabConfigAsync(
        string bootWimPath, int index, InstallationOptionsModel options, string mountDir,
        CancellationToken cancellationToken = default, IProgress<InstallationProgressInfo>? progress = null,
        bool applyOfflineOobeBypass = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bootWimPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountDir);
        ArgumentNullException.ThrowIfNull(options);

        const string mountingStage = "Montando boot.wim";
        const string compatStage = "Aplicando compatibilidad";
        const string validatingStage = "Validando configuración";

        await ValidateBeforeMountAsync(bootWimPath, cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(mountDir);

        _logger.Info("Montando la copia de trabajo de boot.wim (ReadWrite)...");
        progress?.Report(InstallationProgressInfo.Create(mountingStage, 20, "DISM: Mount-Wim de boot.wim iniciado"));
        var mountResult = await _dism.MountWimAsync(bootWimPath, index, mountDir, readOnly: false, cancellationToken)
            .ConfigureAwait(false);

        if (!mountResult.Succeeded)
        {
            progress?.Report(InstallationProgressInfo.Create(
                mountingStage, 20, $"DISM: Mount-Wim falló (ExitCode {mountResult.ExitCode})", InstallationProgressLevel.Error));
            throw new IsoEngineException($"No se pudo montar boot.wim (ExitCode {mountResult.ExitCode}).", mountResult.ExitCode);
        }

        progress?.Report(InstallationProgressInfo.Create(mountingStage, 35, "boot.wim montado", InstallationProgressLevel.Success));

        // Clave temporal única por operación: evita colisionar con un hive que
        // hubiera quedado cargado de una ejecución anterior interrumpida.
        var hiveKeyName = "MRS_ISOENGINE_SYSTEM_" + Guid.NewGuid().ToString("N")[..8];
        var hiveFilePath = Path.Combine(mountDir, "Windows", "System32", "config", "SYSTEM");

        var applied = new List<string>();
        var errors = new List<string>();
        var hiveLoaded = false;
        var success = false;

        try
        {
            progress?.Report(InstallationProgressInfo.Create(compatStage, 40, "Cargando el hive de registro offline..."));
            var loadResult = await _registry.LoadHiveAsync(hiveKeyName, hiveFilePath, cancellationToken).ConfigureAwait(false);
            if (!loadResult.Succeeded)
                throw new IsoEngineException($"No se pudo cargar el hive SYSTEM offline (ExitCode {loadResult.ExitCode}).", loadResult.ExitCode);

            hiveLoaded = true;
            _logger.Info("[COMPAT] LabConfig mounted");

            progress?.Report(InstallationProgressInfo.Create(compatStage, 50, "Aplicando bypasses de compatibilidad..."));
            applied.AddRange(await _labConfigApplier.ApplyAsync(hiveKeyName, options, cancellationToken).ConfigureAwait(false));

            if (applyOfflineOobeBypass)
            {
                progress?.Report(InstallationProgressInfo.Create(compatStage, 60, "Aplicando bypass de red offline (BypassNRO)..."));
                var oobeApplied = await _labConfigApplier.ApplyOfflineOobeBypassAsync(hiveKeyName, cancellationToken).ConfigureAwait(false);
                if (!oobeApplied)
                    throw new IsoEngineException("No se pudo aplicar BypassNRO (OOBE sin conexión) en boot.wim.");

                applied.Add("[COMPAT] BypassNRO applied");
            }

            progress?.Report(InstallationProgressInfo.Create(validatingStage, 75, "Verificando LabConfig..."));
            var verification = await _labConfigApplier.VerifyAsync(hiveKeyName, options, cancellationToken).ConfigureAwait(false);
            var verificationErrors = new List<string>(verification.Errors);

            if (applyOfflineOobeBypass)
            {
                var oobeVerification = await _labConfigApplier.VerifyOfflineOobeBypassAsync(hiveKeyName, cancellationToken).ConfigureAwait(false);
                if (!oobeVerification.IsValid)
                    verificationErrors.AddRange(oobeVerification.Errors);
            }

            if (verificationErrors.Count > 0)
                errors.AddRange(verificationErrors);

            success = verificationErrors.Count == 0;
        }
        catch (IsoEngineException ex)
        {
            errors.Add(ex.Message);
            success = false;
        }
        finally
        {
            // Garantizar limpieza incluso ante excepciones (sección 4/13): el hive
            // siempre se intenta descargar si se llegó a cargar, pase lo que pase arriba.
            if (hiveLoaded)
            {
                var unloadResult = await _registry.UnloadHiveAsync(hiveKeyName, cancellationToken).ConfigureAwait(false);
                if (!unloadResult.Succeeded)
                {
                    errors.Add($"No se pudo descargar el hive de registro offline (ExitCode {unloadResult.ExitCode}).");
                    success = false;
                }
            }
        }

        if (success)
        {
            progress?.Report(InstallationProgressInfo.Create(validatingStage, 85, "Confirmando cambios en boot.wim..."));
            var commit = await _dism.UnmountWimCommitAsync(mountDir, cancellationToken).ConfigureAwait(false);
            if (!commit.Succeeded)
            {
                errors.Add($"No se pudo confirmar boot.wim (ExitCode {commit.ExitCode}).");
                success = false;
            }
            else
            {
                progress?.Report(InstallationProgressInfo.Create(validatingStage, 90, "boot.wim confirmado", InstallationProgressLevel.Success));
            }
        }
        else
        {
            _logger.Warn("Descartando cambios en boot.wim por un fallo previo.");
            progress?.Report(InstallationProgressInfo.Create(
                validatingStage, 85, "Descartando cambios en boot.wim...", InstallationProgressLevel.Warning));
            await _dism.UnmountWimDiscardAsync(mountDir, cancellationToken).ConfigureAwait(false);
        }

        return new BootWimModificationResult(success, applied, errors);
    }

    /// <summary>
    /// P25: comprobaciones baratas antes de invocar Mount-Wim, en el orden de
    /// coste creciente (existencia/tamaño/atributos son solo E/S local; la
    /// consulta a DISM es la única que cuesta un proceso externo, así que se
    /// hace la última y solo si las anteriores ya pasaron). Reutiliza
    /// <see cref="IDismRunner.GetWimInfoAsync(string, CancellationToken)"/>
    /// (ya usado en otros motores, p. ej. el preflight de RemovalEngine) en vez
    /// de duplicar una comprobación de validez de WIM propia.
    /// </summary>
    private async Task ValidateBeforeMountAsync(string bootWimPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(bootWimPath))
            throw new IsoEngineException($"No se encuentra boot.wim en el workspace: '{bootWimPath}'.");

        var info = new FileInfo(bootWimPath);
        if (info.Length < MinimumPlausibleBootWimSizeBytes)
            throw new IsoEngineException(
                $"boot.wim en el workspace es sospechosamente pequeño ({info.Length} bytes); no se intentará montar.");

        // P26: defensa en profundidad -- BootWimProvisioner.PrepareWorkingCopyForMount
        // ya deja el archivo escribible en TODOS sus caminos (recién copiado o ya
        // existente), así que esto no debería disparar nunca en la práctica. Si lo
        // hiciera (por ejemplo, algo ajeno al pipeline dejó el archivo en ese
        // estado), es mejor fallar aquí con un mensaje claro que dejar que DISM lo
        // intente y falle con "WIM open failed with access denied."
        if ((info.Attributes & FileAttributes.ReadOnly) != 0)
            throw new IsoEngineException(
                $"boot.wim en el workspace todavía tiene el atributo ReadOnly: '{bootWimPath}'. No se intentará montar.");

        _logger.Info("Validando boot.wim...");
        var wimInfo = await _dism.GetWimInfoAsync(bootWimPath, cancellationToken).ConfigureAwait(false);
        if (!wimInfo.Succeeded)
            throw new IsoEngineException(
                $"boot.wim en el workspace no es un WIM válido (DISM /Get-WimInfo ExitCode {wimInfo.ExitCode}).", wimInfo.ExitCode);

        _logger.Info("OK boot.wim válido y preparado para montaje.");
    }
}
