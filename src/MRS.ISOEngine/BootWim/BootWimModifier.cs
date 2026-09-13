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
        CancellationToken cancellationToken = default, IProgress<InstallationProgressInfo>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bootWimPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountDir);
        ArgumentNullException.ThrowIfNull(options);

        const string mountingStage = "Montando boot.wim";
        const string compatStage = "Aplicando compatibilidad";
        const string validatingStage = "Validando configuración";

        if (!File.Exists(bootWimPath))
            throw new IsoEngineException($"No se encuentra boot.wim en el workspace: '{bootWimPath}'.");

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

            progress?.Report(InstallationProgressInfo.Create(validatingStage, 75, "Verificando LabConfig..."));
            var verification = await _labConfigApplier.VerifyAsync(hiveKeyName, options, cancellationToken).ConfigureAwait(false);
            if (!verification.IsValid)
                errors.AddRange(verification.Errors);

            success = verification.IsValid;
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
}
