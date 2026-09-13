using MRS.DismEngine.Logging;
using MRS.ISOEngine.Autounattend;
using MRS.ISOEngine.BootWim;
using MRS.ISOEngine.Configuration;
using MRS.ISOEngine.Exceptions;
using MRS.ISOEngine.Models;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine;

/// <summary>Implementación de <see cref="IInstallationImageService"/>. Ver la interfaz para el diagrama de responsabilidad.</summary>
public sealed class InstallationImageService : IInstallationImageService
{
    private readonly IBootWimProvisioner _bootWimProvisioner;
    private readonly IBootWimModifier _bootWimModifier;
    private readonly IAppLogger _logger;

    public InstallationImageService(
        IBootWimProvisioner bootWimProvisioner, IBootWimModifier bootWimModifier, IAppLogger? logger = null)
    {
        _bootWimProvisioner = bootWimProvisioner ?? throw new ArgumentNullException(nameof(bootWimProvisioner));
        _bootWimModifier = bootWimModifier ?? throw new ArgumentNullException(nameof(bootWimModifier));
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task<InstallationImageResult> ApplyAsync(
        string sourceIsoPath, GenerationWorkspace workspace, InstallationOptionsModel options,
        AutounattendConfiguration accountConfig, CancellationToken cancellationToken = default,
        IProgress<InstallationProgressInfo>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIsoPath);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(accountConfig);

        const string preparingStage = "Preparando workspace";
        const string bootWimPrepStage = "Preparando boot.wim";
        const string oobeStage = "Configurando OOBE";
        const string finalizingStage = "Finalizando";

        _logger.Info(
            $"[WORKSPACE] Phase=install-options-inicio WorkspaceId={workspace.WorkspaceId} " +
            $"SourceIsoPath={sourceIsoPath} BootWimPath={workspace.BootWimPath}");
        progress?.Report(InstallationProgressInfo.Create(preparingStage, 2, "Validando configuración de instalación..."));

        // Aborta antes de tocar nada si hay una opción sin mecanismo implementado
        // (P16, sección 9): nunca generar algo aparentemente completo pero engañoso.
        var validation = InstallationExecutionValidator.Validate(options);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors);
            _logger.Error(message);
            progress?.Report(InstallationProgressInfo.Create(preparingStage, 2, message, InstallationProgressLevel.Error));
            return new InstallationImageResult(false, Array.Empty<string>(), validation.Errors, null);
        }

        progress?.Report(InstallationProgressInfo.Create(preparingStage, 10, "Configuración validada", InstallationProgressLevel.Success));

        progress?.Report(InstallationProgressInfo.Create(bootWimPrepStage, 12, "Comprobando copia de boot.wim en el workspace..."));
        await _bootWimProvisioner.EnsureBootWimCopyAsync(sourceIsoPath, workspace.BootWimPath, cancellationToken)
            .ConfigureAwait(false);
        progress?.Report(InstallationProgressInfo.Create(bootWimPrepStage, 20, "boot.wim listo en el workspace", InstallationProgressLevel.Success));

        // boot.wim índice 1 = Windows Setup / WinPE (índice 2 es Recuperación; no se toca).
        var modification = await _bootWimModifier
            .ApplyLabConfigAsync(workspace.BootWimPath, index: 1, options, workspace.MountPath, cancellationToken, progress)
            .ConfigureAwait(false);

        var errors = new List<string>(modification.Errors);
        var applied = new List<string>(modification.AppliedLogLines);
        string? autounattendPath = null;

        if (modification.Success)
        {
            var autounattendFilePath = Path.Combine(workspace.WorkspacePath, "autounattend.xml");

            if (options.AllowLocalAccount)
            {
                progress?.Report(InstallationProgressInfo.Create(oobeStage, 92, "Generando autounattend.xml..."));
                try
                {
                    // Determinista: siempre sobrescribe el mismo archivo, nunca duplica.
                    var xml = AutounattendGenerator.Generate(accountConfig, options.AllowOfflineOobe);
                    File.WriteAllText(autounattendFilePath, xml);
                    autounattendPath = autounattendFilePath;

                    const string line = "[INSTALL] Local account enabled";
                    applied.Add(line);
                    _logger.Info(line);
                    progress?.Report(InstallationProgressInfo.Create(oobeStage, 96, "autounattend.xml generado", InstallationProgressLevel.Success));
                }
                catch (IsoEngineException ex)
                {
                    errors.Add(ex.Message);
                }
            }
            else if (File.Exists(autounattendFilePath))
            {
                // Cuenta local desactivada: no debe quedar un autounattend.xml de una
                // ejecución anterior imponiendo una cuenta local no deseada.
                File.Delete(autounattendFilePath);
                _logger.Info("autounattend.xml eliminado (cuenta local desactivada).");
            }
        }

        var success = modification.Success && errors.Count == 0;
        progress?.Report(InstallationProgressInfo.Create(
            finalizingStage, 100,
            success ? "Configuración de instalación aplicada" : "La configuración de instalación no se completó",
            success ? InstallationProgressLevel.Success : InstallationProgressLevel.Error));

        _logger.Info($"[WORKSPACE] Phase=install-options-fin WorkspaceId={workspace.WorkspaceId} Success={success}");

        return new InstallationImageResult(success, applied, errors, autounattendPath);
    }
}
