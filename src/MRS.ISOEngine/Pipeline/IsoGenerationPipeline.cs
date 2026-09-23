using MRS.DismEngine.Logging;
using MRS.ImageEngine.Iso;
using MRS.ISOEngine.Configuration;
using MRS.ISOEngine.InstallWim;
using MRS.ISOEngine.Models;
using MRS.ISOEngine.Oscdimg;
using MRS.ISOEngine.TreeCopy;
using MRS.PostInstall.Models;
using MRS.PostInstall.Packaging;
using MRS.RemovalEngine;
using MRS.RemovalPlanning.Models;

namespace MRS.ISOEngine.Pipeline;

/// <summary>Implementación de <see cref="IIsoGenerationPipeline"/>. Ver la interfaz para el diagrama completo del pipeline (P19).</summary>
public sealed class IsoGenerationPipeline : IIsoGenerationPipeline
{
    private readonly IIsoTreeCopier _treeCopier;
    private readonly IIsoMounter _isoMounter;
    private readonly IInstallationImageService _installationImageService;
    private readonly IWorkingImageFactory _workingImageFactory;
    private readonly IRemovalEngine _removalEngine;
    private readonly IInstallWimOobeConfigurator _installWimOobeConfigurator;
    private readonly IPostInstallPackageBuilder _postInstallPackageBuilder;
    private readonly IOscdimgRunner _oscdimgRunner;
    private readonly IElevationChecker _elevationChecker;
    private readonly IAppLogger _logger;

    public IsoGenerationPipeline(
        IIsoTreeCopier treeCopier,
        IIsoMounter isoMounter,
        IInstallationImageService installationImageService,
        IWorkingImageFactory workingImageFactory,
        IRemovalEngine removalEngine,
        IInstallWimOobeConfigurator installWimOobeConfigurator,
        IPostInstallPackageBuilder postInstallPackageBuilder,
        IOscdimgRunner oscdimgRunner,
        IElevationChecker? elevationChecker = null,
        IAppLogger? logger = null)
    {
        _treeCopier = treeCopier ?? throw new ArgumentNullException(nameof(treeCopier));
        _isoMounter = isoMounter ?? throw new ArgumentNullException(nameof(isoMounter));
        _installationImageService = installationImageService ?? throw new ArgumentNullException(nameof(installationImageService));
        _workingImageFactory = workingImageFactory ?? throw new ArgumentNullException(nameof(workingImageFactory));
        _removalEngine = removalEngine ?? throw new ArgumentNullException(nameof(removalEngine));
        _installWimOobeConfigurator = installWimOobeConfigurator ?? throw new ArgumentNullException(nameof(installWimOobeConfigurator));
        _postInstallPackageBuilder = postInstallPackageBuilder ?? throw new ArgumentNullException(nameof(postInstallPackageBuilder));
        _oscdimgRunner = oscdimgRunner ?? throw new ArgumentNullException(nameof(oscdimgRunner));
        _elevationChecker = elevationChecker ?? new ElevationChecker();
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task<IsoGenerationResult> GenerateAsync(
        IsoGenerationRequest request, CancellationToken cancellationToken = default,
        IProgress<InstallationProgressInfo>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        const string environmentStage = "Comprobación de entorno";
        const string validatingStage = "Validación";
        const string workspaceStage = "Generation workspace";
        const string treeStage = "Preparando árbol de la ISO";
        const string bootWimStage = "Modificando boot.wim";
        const string installWimStage = "Modificando install.wim";
        const string postInstallStage = "Integración PostInstall";
        const string finalValidationStage = "Validación final";
        const string oscdimgStage = "Generando ISO (oscdimg)";
        const string finalizingStage = "Finalizando";

        var appliedLines = new List<string>();
        var errors = new List<string>();
        GenerationWorkspace? workspace = null;

        // P20, sección 1: comprobar el entorno ANTES que la propia solicitud.
        // DISM exige privilegios de administrador incluso para lecturas, y
        // oscdimg requiere el Windows ADK; sin ambos, ninguna fase real del
        // pipeline puede completarse, así que se aborta aquí con los mensajes
        // exactos exigidos en lugar de fallar de forma confusa a mitad de
        // PrepareBootWim/PrepareInstallWim u oscdimg.
        progress?.Report(InstallationProgressInfo.Create(environmentStage, 0, "Comprobando privilegios y Windows ADK..."));
        var environment = EnvironmentPreflightChecker.Check(_elevationChecker, _oscdimgRunner);
        if (!environment.IsReady)
        {
            var message = string.Join(" ", environment.Errors);
            _logger.Error($"[PIPELINE] {message}");
            progress?.Report(InstallationProgressInfo.Create(environmentStage, 0, message, InstallationProgressLevel.Error));
            return new IsoGenerationResult(false, null, null, appliedLines, environment.Errors);
        }
        progress?.Report(InstallationProgressInfo.Create(environmentStage, 1, "Entorno listo (elevado, ADK disponible)", InstallationProgressLevel.Success));

        progress?.Report(InstallationProgressInfo.Create(validatingStage, 1, "Validando solicitud..."));
        var requestValidation = IsoGenerationRequestValidator.Validate(request);
        if (!requestValidation.IsValid)
        {
            var message = string.Join(" ", requestValidation.Errors);
            _logger.Error($"[PIPELINE] {message}");
            progress?.Report(InstallationProgressInfo.Create(validatingStage, 1, message, InstallationProgressLevel.Error));
            return new IsoGenerationResult(false, null, null, appliedLines, requestValidation.Errors);
        }
        progress?.Report(InstallationProgressInfo.Create(validatingStage, 5, "Solicitud válida", InstallationProgressLevel.Success));

        try
        {
            progress?.Report(InstallationProgressInfo.Create(workspaceStage, 6, "Creando workspace de generación..."));
            workspace = GenerationWorkspaceFactory.CreateNew(request.SourceIsoPath, request.EditionIndex, request.Architecture);
            _logger.Info($"[WORKSPACE] Phase=iso-generation-workspace-creado WorkspaceId={workspace.WorkspaceId} SourceIsoPath={request.SourceIsoPath}");
            progress?.Report(InstallationProgressInfo.Create(workspaceStage, 10, "Workspace creado", InstallationProgressLevel.Success));

            // ---- PREPARACIÓN: árbol completo de la ISO ------------------------
            progress?.Report(InstallationProgressInfo.Create(treeStage, 12, "Copiando el árbol completo de la ISO..."));
            await _treeCopier.CopyAsync(request.SourceIsoPath, workspace.WorkspacePath, cancellationToken).ConfigureAwait(false);
            progress?.Report(InstallationProgressInfo.Create(treeStage, 30, "Árbol de la ISO copiado", InstallationProgressLevel.Success));

            // ---- MODIFICACIÓN BOOT.WIM: LabConfig + Setup/OOBE -----------------
            progress?.Report(InstallationProgressInfo.Create(bootWimStage, 32, "Aplicando LabConfig y generando autounattend.xml..."));
            var installationResult = await _installationImageService
                .ApplyAsync(request.SourceIsoPath, workspace, request.InstallationOptions, request.AccountConfiguration, cancellationToken, progress)
                .ConfigureAwait(false);

            appliedLines.AddRange(installationResult.AppliedLogLines);
            if (!installationResult.Success)
            {
                errors.AddRange(installationResult.Errors);
                _logger.Error("[PIPELINE] boot.wim modification failed: " + string.Join(" ", installationResult.Errors));
                progress?.Report(InstallationProgressInfo.Create(bootWimStage, 55, "Falló la modificación de boot.wim", InstallationProgressLevel.Error));
                return new IsoGenerationResult(false, null, workspace, appliedLines, errors);
            }
            progress?.Report(InstallationProgressInfo.Create(bootWimStage, 55, "boot.wim modificado", InstallationProgressLevel.Success));

            // ---- MODIFICACIÓN INSTALL.WIM: selección Pro + RemovalPlan --------
            progress?.Report(InstallationProgressInfo.Create(installWimStage, 57, "Montando la ISO de origen para exportar install.wim..."));
            var isoMount = await _isoMounter.MountAsync(request.SourceIsoPath, cancellationToken).ConfigureAwait(false);

            string? sourceInstallWimPath = null;
            MRS.RemovalEngine.Models.WorkingImage workingImage;
            MRS.RemovalEngine.Models.RemovalExecutionResult removalResult;
            try
            {
                var sourceInstallWim = FindInstallWim(isoMount.RootPath);
                sourceInstallWimPath = sourceInstallWim;
                if (sourceInstallWim is null)
                {
                    const string message = @"La ISO no contiene sources\install.wim ni sources\install.esd.";
                    errors.Add(message);
                    _logger.Error($"[PIPELINE] {message}");
                    progress?.Report(InstallationProgressInfo.Create(installWimStage, 57, message, InstallationProgressLevel.Error));
                    return new IsoGenerationResult(false, null, workspace, appliedLines, errors);
                }

                var innerProgress = new Progress<MRS.RemovalEngine.Models.ProgressInfo>(p =>
                    progress?.Report(InstallationProgressInfo.Create(installWimStage, Rescale(p.Percent, 60, 78), p.Message, MapRemovalLevel(p.Level))));

                workingImage = await _workingImageFactory
                    .CreateAsync(sourceInstallWim, request.EditionIndex, workspaceRoot: null, cancellationToken, innerProgress)
                    .ConfigureAwait(false);

                // P19 nunca decide qué eliminar: si no se proporciona un plan, se
                // aplica un plan vacío (solo selección de la edición Pro).
                var plan = request.RemovalPlan ?? new RemovalPlan();
                removalResult = await _removalEngine
                    .ExecuteAsync(workingImage, plan, cancellationToken, innerProgress)
                    .ConfigureAwait(false);
            }
            finally
            {
                await isoMount.DisposeAsync().ConfigureAwait(false);
            }

            if (!removalResult.Success)
            {
                errors.AddRange(removalResult.Errors);
                _logger.Error("[PIPELINE] install.wim modification failed: " + string.Join(" ", removalResult.Errors));
                progress?.Report(InstallationProgressInfo.Create(installWimStage, 78, "Falló la modificación de install.wim", InstallationProgressLevel.Error));
                return new IsoGenerationResult(false, null, workspace, appliedLines, errors);
            }

            // P29: el BypassNRO offline de boot.wim (P28) no basta -- la prueba
            // real en VM siguió mostrando "Vamos a conectarte a una red". El
            // estado necesario tiene que existir también en el SYSTEM hive del
            // Windows YA INSTALADO antes de llegar a OOBE, así que se aplica aquí,
            // sobre la MISMA imagen de trabajo que ya usa el pipeline (nunca sobre
            // la ISO original ni sobre una copia distinta), justo después de que
            // RemovalEngine terminó y antes de copiarla al workspace de generación.
            if (request.InstallationOptions.AllowOfflineOobe)
            {
                var installWimOobeResult = await _installWimOobeConfigurator
                    .ApplyOfflineOobeBypassAsync(workingImage.WorkingWimPath, workingImage.Index, workingImage.MountPath, cancellationToken, progress)
                    .ConfigureAwait(false);

                appliedLines.AddRange(installWimOobeResult.AppliedLogLines);
                if (!installWimOobeResult.Success)
                {
                    errors.AddRange(installWimOobeResult.Errors);
                    _logger.Error("[PIPELINE] install.wim OOBE configuration failed: " + string.Join(" ", installWimOobeResult.Errors));
                    progress?.Report(InstallationProgressInfo.Create(installWimStage, 79, "Falló la configuración OOBE offline de install.wim", InstallationProgressLevel.Error));
                    return new IsoGenerationResult(false, null, workspace, appliedLines, errors);
                }
            }

            // El install.wim modificado vive en el workspace propio de
            // WorkingImageFactory (P07); se copia al workspace de generación de
            // P19 para que quede junto a boot.wim/$OEM$/autounattend.xml.
            _logger.Info($"[INSTALL] Source install.wim (ISO original, montada de solo lectura): '{sourceInstallWimPath}'");
            _logger.Info($"[INSTALL] Working install.wim (WorkingImageFactory + RemovalEngine): '{workingImage.WorkingWimPath}'");
            _logger.Info($"[INSTALL] Final ISO install.wim (workspace de generación): '{workspace.InstallWimPath}'");

            // P27: sources\install.wim en el workspace de generación ya lo copió
            // IsoTreeCopier (junto con el resto del árbol de la ISO, antes de
            // llegar aquí) desde la ISO montada en solo lectura -- igual que
            // boot.wim en P25/P26, esa copia hereda el atributo ReadOnly.
            // File.Copy con overwrite:true NO lo quita por sí solo: falla con
            // "Access to the path ... is denied." al intentar sustituirlo por la
            // imagen de trabajo ya terminada. Se prepara aquí, justo antes de la
            // sustitución final -- solo sobre la copia del workspace de
            // generación, nunca sobre la ISO montada ni sobre la imagen de
            // trabajo de WorkingImageFactory, y solo después de que
            // removalResult.Success ya sea true (la sustitución nunca ocurre si
            // la imagen de trabajo no terminó correctamente).
            EnsureInstallWimDestinationWritable(workspace.InstallWimPath);
            File.Copy(workingImage.WorkingWimPath, workspace.InstallWimPath, overwrite: true);
            appliedLines.Add($"[INSTALL] install.wim exportado (índice {request.EditionIndex}) con {removalResult.ActionsExecuted.Count} acción(es) aplicada(s)");
            progress?.Report(InstallationProgressInfo.Create(installWimStage, 80, "install.wim modificado", InstallationProgressLevel.Success));

            // ---- INTEGRACIÓN POSTINSTALL: .NET + PCPI --------------------------
            if (request.PostInstallConfiguration.Enabled)
            {
                progress?.Report(InstallationProgressInfo.Create(postInstallStage, 82, "Empaquetando PostInstall..."));
                var postInstallOutput = Path.Combine(Path.GetTempPath(), "mrs-postinstall-" + workspace.WorkspaceId);

                var postInstallProgress = new Progress<PostInstallProgressInfo>(p =>
                    progress?.Report(InstallationProgressInfo.Create(postInstallStage, Rescale(p.Percent, 82, 88), p.Message, MapPostInstallLevel(p.Level))));

                var postInstallResult = _postInstallPackageBuilder.Build(
                    request.PostInstallConfiguration, request.PostInstallSourceFiles, postInstallOutput, postInstallProgress);

                if (!postInstallResult.Success)
                {
                    errors.AddRange(postInstallResult.Errors);
                    _logger.Error("[PIPELINE] PostInstall packaging failed: " + string.Join(" ", postInstallResult.Errors));
                    progress?.Report(InstallationProgressInfo.Create(postInstallStage, 88, "Falló el empaquetado de PostInstall", InstallationProgressLevel.Error));
                    return new IsoGenerationResult(false, null, workspace, appliedLines, errors);
                }

                if (postInstallResult.OemRootPath is not null)
                {
                    var destinationOem = Path.Combine(workspace.WorkspacePath, "sources", "$OEM$");
                    DirectoryCopyHelper.CopyAll(postInstallResult.OemRootPath, destinationOem, cancellationToken);
                    appliedLines.Add(@"[POSTINSTALL] Paquete integrado en sources\$OEM$");
                }

                try { Directory.Delete(postInstallOutput, recursive: true); }
                catch { /* directorio temporal, ya copiado a workspace; best-effort */ }

                progress?.Report(InstallationProgressInfo.Create(postInstallStage, 88, "PostInstall integrado", InstallationProgressLevel.Success));
            }
            else
            {
                _logger.Info("[PIPELINE] PostInstall deshabilitado: no se integra ningún paquete.");
            }

            // ---- VALIDACIÓN final -----------------------------------------------
            progress?.Report(InstallationProgressInfo.Create(finalValidationStage, 90, "Validando el workspace final..."));
            var finalValidation = GenerationWorkspaceValidator.Validate(workspace);
            if (!finalValidation.IsValid)
            {
                errors.AddRange(finalValidation.Errors);
                _logger.Error("[PIPELINE] Final workspace validation failed: " + string.Join(" ", finalValidation.Errors));
                progress?.Report(InstallationProgressInfo.Create(finalValidationStage, 90, "Validación final fallida", InstallationProgressLevel.Error));
                return new IsoGenerationResult(false, null, workspace, appliedLines, errors);
            }

            // P28: re-verifica, de forma independiente y tras el commit, boot.wim
            // (índices 1 y 2) y autounattend.xml -- nunca se acepta el resultado ya
            // reportado por PrepareBootWim como prueba suficiente.
            progress?.Report(InstallationProgressInfo.Create(finalValidationStage, 91, "Verificando boot.wim y autounattend.xml..."));
            var finalCompatValidation = await _installationImageService
                .ValidateFinalAsync(workspace, request.InstallationOptions, request.AccountConfiguration, cancellationToken)
                .ConfigureAwait(false);
            if (!finalCompatValidation.IsValid)
            {
                errors.AddRange(finalCompatValidation.Errors);
                _logger.Error("[PIPELINE] Final compatibility validation failed: " + string.Join(" ", finalCompatValidation.Errors));
                progress?.Report(InstallationProgressInfo.Create(finalValidationStage, 91, "Validación final de compatibilidad fallida", InstallationProgressLevel.Error));
                return new IsoGenerationResult(false, null, workspace, appliedLines, errors);
            }

            progress?.Report(InstallationProgressInfo.Create(finalValidationStage, 92, "Workspace válido", InstallationProgressLevel.Success));

            // ---- OSCDIMG ----------------------------------------------------------
            progress?.Report(InstallationProgressInfo.Create(oscdimgStage, 94, "Generando la ISO final (oscdimg)..."));
            if (!_oscdimgRunner.IsAvailable())
            {
                const string message =
                    "oscdimg.exe no está disponible en este equipo (Windows ADK — Deployment Tools no instalado). " +
                    "El workspace queda preparado y listo; la generación final de la ISO queda pendiente.";
                errors.Add(message);
                _logger.Error($"[PIPELINE] {message}");
                progress?.Report(InstallationProgressInfo.Create(oscdimgStage, 94, message, InstallationProgressLevel.Error));
                return new IsoGenerationResult(false, null, workspace, appliedLines, errors);
            }

            var oscdimgResult = await _oscdimgRunner
                .BuildIsoAsync(workspace.WorkspacePath, request.OutputIsoPath, cancellationToken)
                .ConfigureAwait(false);

            if (!oscdimgResult.Succeeded)
            {
                var message = $"oscdimg falló (ExitCode {oscdimgResult.ExitCode}).";
                errors.Add(message);
                _logger.Error($"[PIPELINE] {message}");
                progress?.Report(InstallationProgressInfo.Create(oscdimgStage, 96, message, InstallationProgressLevel.Error));
                return new IsoGenerationResult(false, null, workspace, appliedLines, errors);
            }

            appliedLines.Add($"[ISO] ISO final generada: {request.OutputIsoPath}");
            progress?.Report(InstallationProgressInfo.Create(finalizingStage, 100, "ISO final generada", InstallationProgressLevel.Success));

            return new IsoGenerationResult(true, request.OutputIsoPath, workspace, appliedLines, errors);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error($"[PIPELINE] Unexpected failure: {ex.Message}");
            errors.Add(ex.Message);
            return new IsoGenerationResult(false, null, workspace, appliedLines, errors);
        }
    }

    /// <summary>
    /// P27: quita ReadOnly de <c>workspace\sources\install.wim</c> si lo tiene,
    /// antes de sustituirlo por la imagen de trabajo ya terminada. Idempotente
    /// (si ya es escribible, o si el archivo todavía no existe, no hace nada).
    /// Nunca se llama sobre la ISO montada ni sobre la imagen de trabajo de
    /// WorkingImageFactory -- solo sobre esta ruta concreta del workspace de
    /// generación (mismo patrón que <c>BootWimProvisioner.PrepareWorkingCopyForMount</c>,
    /// P25/P26, aplicado aquí a la sustitución final en vez de al premontaje).
    /// </summary>
    private void EnsureInstallWimDestinationWritable(string path)
    {
        if (!File.Exists(path))
            return;

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) == 0)
            return;

        _logger.Info("Eliminando atributo ReadOnly de install.wim en el workspace de generación...");
        File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }

    private static string? FindInstallWim(string mountRoot)
    {
        foreach (var name in new[] { "install.wim", "install.esd" })
        {
            var candidate = Path.Combine(mountRoot, "sources", name);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static int Rescale(int percent, int low, int high) => low + percent * (high - low) / 100;

    private static InstallationProgressLevel MapRemovalLevel(MRS.RemovalEngine.Models.ProgressLevel level) => level switch
    {
        MRS.RemovalEngine.Models.ProgressLevel.Success => InstallationProgressLevel.Success,
        MRS.RemovalEngine.Models.ProgressLevel.Warning => InstallationProgressLevel.Warning,
        MRS.RemovalEngine.Models.ProgressLevel.Error => InstallationProgressLevel.Error,
        _ => InstallationProgressLevel.Info,
    };

    private static InstallationProgressLevel MapPostInstallLevel(PostInstallProgressLevel level) => level switch
    {
        PostInstallProgressLevel.Success => InstallationProgressLevel.Success,
        PostInstallProgressLevel.Warning => InstallationProgressLevel.Warning,
        PostInstallProgressLevel.Error => InstallationProgressLevel.Error,
        _ => InstallationProgressLevel.Info,
    };
}
