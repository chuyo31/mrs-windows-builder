using MRS.ComponentCatalog.Models;
using MRS.DismEngine.Dism;
using MRS.DismEngine.Logging;
using MRS.DismEngine.Processes;
using MRS.ImageEngine.Inventory;
using MRS.RemovalEngine.Models;
using MRS.RemovalPlanning.Models;

namespace MRS.RemovalEngine;

/// <summary>
/// Ejecuta un <see cref="RemovalPlan"/> ya construido y validado contra una
/// <see cref="WorkingImage"/> (nunca contra la ISO original).
///
/// Comportamiento transaccional: si TODO sale bien se hace
/// <c>/Unmount-Wim /Commit</c>; ante el primer fallo (o una cancelación) se
/// hace <c>/Unmount-Wim /Discard</c> y no se conserva ningún cambio parcial.
/// Nunca usa <c>/ResetBase</c>, <c>/StartComponentCleanup</c> ni
/// <c>/Cleanup-Image</c>.
/// </summary>
public sealed class RemovalEngine : IRemovalEngine
{
    private static readonly RemovalActionType[] ExecutionOrder =
    {
        RemovalActionType.RemoveAppx,
        RemovalActionType.DisableFeature,
        RemovalActionType.RemoveCapability,
        RemovalActionType.RemovePackage,
    };

    private readonly IDismRunner _dism;
    private readonly IAppLogger _logger;

    public RemovalEngine(IDismRunner dismRunner, IAppLogger? logger = null)
    {
        _dism = dismRunner ?? throw new ArgumentNullException(nameof(dismRunner));
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task<RemovalExecutionResult> ExecuteAsync(
        WorkingImage image, RemovalPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(plan);

        var executed = new List<RemovalExecutionItem>();
        var failed = new List<RemovalExecutionItem>();
        var warnings = new List<string>();
        var errors = new List<string>();

        // ---- 1) PRE-FLIGHT ------------------------------------------------

        LogContext("preflight-inicio", image);

        if (!image.HasConsistentWorkspace())
        {
            // MountPath o WorkingWimPath pertenecen a un workspace distinto de
            // WorkspacePath: exactamente el problema que investigó P09. Se aborta
            // antes de montar nada en vez de arriesgarse a mezclar contextos.
            errors.Add(
                $"La imagen de trabajo mezcla rutas de workspaces distintos " +
                $"(WorkspaceId esperado: {image.WorkspaceId}; MountPath: {image.MountPath}; WorkingWimPath: {image.WorkingWimPath}).");
            _logger.Error($"[WORKSPACE] Contexto inconsistente detectado. {DescribeContext(image)}");
            return BuildResult(RemovalExecutionPhase.PreFlight, false, image, executed, failed, warnings, errors, false, false);
        }

        if (!plan.IsValid)
        {
            errors.Add("El RemovalPlan no es válido (contiene errores de construcción).");
            return BuildResult(RemovalExecutionPhase.PreFlight, false, image, executed, failed, warnings, errors, false, false);
        }

        if (!File.Exists(image.WorkingWimPath))
        {
            errors.Add("No existe la imagen de trabajo (WorkingWim).");
            return BuildResult(RemovalExecutionPhase.PreFlight, false, image, executed, failed, warnings, errors, false, false);
        }

        var indexCheck = await _dism.GetWimInfoAsync(image.WorkingWimPath, image.Index, cancellationToken).ConfigureAwait(false);
        if (!indexCheck.Succeeded)
        {
            errors.Add($"El índice {image.Index} no existe en la imagen de trabajo.");
            return BuildResult(RemovalExecutionPhase.PreFlight, false, image, executed, failed, warnings, errors, false, false);
        }

        if (!HasEnoughFreeSpace(image.WorkspacePath))
        {
            errors.Add("Espacio en disco insuficiente para continuar.");
            return BuildResult(RemovalExecutionPhase.PreFlight, false, image, executed, failed, warnings, errors, false, false);
        }

        await RecoverOrphanMountsAsync(image, cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            warnings.Add("Operación cancelada. La imagen de trabajo no ha sido modificada.");
            return BuildResult(RemovalExecutionPhase.Cancelled, false, image, executed, failed, warnings, errors, false, false);
        }

        // ---- 2) MONTAJE READWRITE -----------------------------------------

        _logger.Info($"Montando imagen de trabajo (índice {image.Index}, lectura/escritura)...");
        var mount = await _dism.MountWimAsync(image.WorkingWimPath, image.Index, image.MountPath, readOnly: false, cancellationToken).ConfigureAwait(false);
        if (!mount.Succeeded)
        {
            errors.Add($"No se pudo montar la imagen de trabajo. ExitCode: {mount.ExitCode}");
            return BuildResult(RemovalExecutionPhase.Mounting, false, image, executed, failed, warnings, errors, false, false);
        }
        image.IsMounted = true;
        LogContext("montada", image);

        // ---- 3) EJECUCIÓN (AppX -> Features -> Capabilities -> Packages) -

        var itemsById = plan.Components.ToDictionary(i => i.ComponentId, StringComparer.Ordinal);
        var cancelled = false;

        foreach (var action in OrderActions(plan.Actions))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            if (!TryVerifySafety(action, itemsById, out var blockReason))
            {
                var blocked = new RemovalExecutionItem
                {
                    ComponentId = action.ComponentId,
                    ActionType = action.ActionType,
                    Target = action.Target,
                    StartedAt = DateTimeOffset.UtcNow,
                    FinishedAt = DateTimeOffset.UtcNow,
                    Success = false,
                    Error = blockReason,
                };
                failed.Add(blocked);
                errors.Add(blockReason);
                _logger.Error($"[REMOVAL] ERROR: {blockReason}");
                _logger.Error("[REMOVAL] Abortando ejecución.");
                break;
            }

            var displayName = itemsById.TryGetValue(action.ComponentId, out var planItem) ? planItem.DisplayName : action.ComponentId;
            _logger.Info($"[REMOVAL] Inicio: {displayName}");
            _logger.Info($"[REMOVAL] Action: {action.ActionType}");
            LogContext($"ejecutando:{action.ComponentId}", image);

            var startedAt = DateTimeOffset.UtcNow;
            ProcessRunResult result;
            try
            {
                result = await ExecuteActionAsync(action, image.MountPath, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                break;
            }

            var item = new RemovalExecutionItem
            {
                ComponentId = action.ComponentId,
                ActionType = action.ActionType,
                Target = action.Target,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow,
                Success = result.Succeeded,
                ExitCode = result.ExitCode,
                Output = Trim(result.StandardOutput),
                Error = result.Succeeded ? null : Trim(result.StandardError),
            };

            if (result.Succeeded)
            {
                executed.Add(item);
                _logger.Info($"[REMOVAL] {displayName} eliminado correctamente.");
            }
            else
            {
                failed.Add(item);
                errors.Add($"{displayName}: ExitCode {result.ExitCode}");
                _logger.Error($"[REMOVAL] ERROR: {displayName} falló.");
                _logger.Error($"[DISM] ExitCode={result.ExitCode}");
                _logger.Error("[REMOVAL] Abortando ejecución.");
                break; // Parte 19: el primer error aborta; nunca reintentos destructivos.
            }
        }

        // ---- 4) CANCELACIÓN / ERROR -> DISCARD; TODO OK -> COMMIT ----------

        if (cancelled)
        {
            warnings.Add("Operación cancelada. La imagen de trabajo no ha sido modificada.");
            await DiscardAsync(image).ConfigureAwait(false);
            return BuildResult(RemovalExecutionPhase.Cancelled, false, image, executed, failed, warnings, errors, false, true);
        }

        if (failed.Count > 0)
        {
            _logger.Info("[REMOVAL] Descartando imagen.");
            await DiscardAsync(image).ConfigureAwait(false);
            return BuildResult(RemovalExecutionPhase.Failed, false, image, executed, failed, warnings, errors, false, true);
        }

        _logger.Info("Confirmando cambios (commit)...");
        LogContext("commit-inicio", image);
        var commit = await _dism.UnmountWimCommitAsync(image.MountPath, cancellationToken).ConfigureAwait(false);
        image.IsMounted = false;

        if (!commit.Succeeded)
        {
            // Idempotencia (Parte "limpieza idempotente" de P08): un ExitCode != 0 en
            // /Unmount-Wim /Commit no es necesariamente un fallo real si el montaje ya
            // no existe (p. ej. una operación previa ya lo desmontó). Nunca se asume
            // éxito solo por esto: se comprueba explícitamente con Get-MountedWimInfo.
            var stillMounted = await IsStillMountedAsync(image.MountPath).ConfigureAwait(false);
            if (!stillMounted)
            {
                _logger.Warn($"El commit devolvió ExitCode {commit.ExitCode}, pero la imagen ya no aparece montada; se considera confirmada.");
                image.IsCommitted = true;
                _logger.Info("[REMOVAL] Cambios aplicados y confirmados correctamente.");
                return BuildResult(RemovalExecutionPhase.Completed, true, image, executed, failed, warnings, errors, true, false);
            }

            errors.Add($"No se pudo confirmar (commit) la imagen. ExitCode: {commit.ExitCode}");
            _logger.Error($"[REMOVAL] ERROR: commit falló. ExitCode={commit.ExitCode}");
            return BuildResult(RemovalExecutionPhase.Failed, false, image, executed, failed, warnings, errors, false, false);
        }

        image.IsCommitted = true;
        LogContext("commit-confirmado", image);
        await VerifyNoMountsRemainAsync(image.MountPath).ConfigureAwait(false);
        _logger.Info("[REMOVAL] Cambios aplicados y confirmados correctamente.");

        return BuildResult(RemovalExecutionPhase.Completed, true, image, executed, failed, warnings, errors, true, false);
    }

    /// <summary>
    /// Log estructurado (OperationId/WorkspaceId/SourceWimPath/WorkingWimPath/MountDir)
    /// de una única operación de <see cref="RemovalEngine"/>. El OperationId es el
    /// WorkspaceId de la imagen de trabajo: toda la operación usa siempre ese
    /// mismo workspace de principio a fin, nunca uno distinto a mitad de camino.
    /// </summary>
    private void LogContext(string phase, WorkingImage image)
        => _logger.Info($"[WORKSPACE] {DescribeContext(image, phase)}");

    private static string DescribeContext(WorkingImage image, string? phase = null)
        => $"{(phase is null ? string.Empty : $"Phase={phase} ")}OperationId={image.WorkspaceId} " +
           $"WorkspaceId={image.WorkspaceId} SourceWimPath={image.SourcePath} " +
           $"WorkingWimPath={image.WorkingWimPath} MountDir={image.MountPath}";

    // ---- Helpers -------------------------------------------------------------

    private static IEnumerable<RemovalAction> OrderActions(IEnumerable<RemovalAction> actions)
        => ExecutionOrder.SelectMany(type => actions.Where(a => a.ActionType == type));

    private static bool TryVerifySafety(
        RemovalAction action, IReadOnlyDictionary<string, RemovalPlanItem> itemsById, out string reason)
    {
        if (!itemsById.TryGetValue(action.ComponentId, out var item))
        {
            reason = $"El componente '{action.ComponentId}' no está presente en el plan.";
            return false;
        }

        if (!item.Allowed)
        {
            reason = $"El componente '{action.ComponentId}' no está permitido en el plan.";
            return false;
        }

        if (item.Protection == ComponentProtection.Protected)
        {
            reason = $"El componente '{action.ComponentId}' está protegido.";
            return false;
        }

        if (item.Action != action.ActionType)
        {
            reason = $"La acción '{action.ActionType}' no coincide con la decidida por el plan ('{item.Action}').";
            return false;
        }

        if (!IsActionCompatible(action.ActionType, item.SourceType))
        {
            reason = $"La acción '{action.ActionType}' no es compatible con el origen '{item.SourceType}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(action.Target))
        {
            reason = "El target de la acción está vacío.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool IsActionCompatible(RemovalActionType action, ComponentSourceType sourceType) => (action, sourceType) switch
    {
        (RemovalActionType.RemovePackage, ComponentSourceType.Package) => true,
        (RemovalActionType.RemoveAppx, ComponentSourceType.Appx) => true,
        (RemovalActionType.DisableFeature, ComponentSourceType.Feature) => true,
        (RemovalActionType.RemoveCapability, ComponentSourceType.Capability) => true,
        _ => false,
    };

    private Task<ProcessRunResult> ExecuteActionAsync(RemovalAction action, string mountPath, CancellationToken cancellationToken) => action.ActionType switch
    {
        RemovalActionType.RemoveAppx => _dism.RemoveProvisionedAppxPackageAsync(mountPath, action.Target, cancellationToken),
        RemovalActionType.DisableFeature => _dism.DisableFeatureAsync(mountPath, action.Target, cancellationToken),
        RemovalActionType.RemoveCapability => _dism.RemoveCapabilityAsync(mountPath, action.Target, cancellationToken),
        RemovalActionType.RemovePackage => _dism.RemovePackageAsync(mountPath, action.Target, cancellationToken),
        _ => throw new InvalidOperationException($"Acción no soportada por el motor: {action.ActionType}"),
    };

    private async Task DiscardAsync(WorkingImage image)
    {
        LogContext("discard-inicio", image);
        try
        {
            // Idempotente: si ya no está montada (p. ej. un finally repetido, o un
            // commit/discard previo que ya la liberó), no hay nada que descartar y no
            // se trata como error.
            if (!image.IsMounted)
            {
                _logger.Info("La imagen ya estaba desmontada; no hay nada que descartar.");
                await VerifyNoMountsRemainAsync(image.MountPath).ConfigureAwait(false);
                return;
            }

            var unmount = await _dism.UnmountWimDiscardAsync(image.MountPath, CancellationToken.None).ConfigureAwait(false);
            image.IsMounted = false;

            if (!unmount.Succeeded)
            {
                var stillMounted = await IsStillMountedAsync(image.MountPath).ConfigureAwait(false);
                if (stillMounted)
                    _logger.Error($"No se pudo desmontar (discard) la imagen. ExitCode: {unmount.ExitCode}");
                else
                    _logger.Info($"El discard devolvió ExitCode {unmount.ExitCode}, pero la imagen ya no aparece montada; se considera desmontada.");
            }

            await VerifyNoMountsRemainAsync(image.MountPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error al descartar la imagen: {ex.Message}");
        }
    }

    private async Task<bool> IsStillMountedAsync(string mountDir)
    {
        try
        {
            var info = await _dism.GetMountedWimInfoAsync(CancellationToken.None).ConfigureAwait(false);
            // Si no se puede consultar el estado, no se asume que ya está desmontada:
            // se prefiere no ocultar un posible error real.
            return !info.Succeeded || MountedWimInfo.ContainsMountDir(info.StandardOutput, mountDir);
        }
        catch (Exception ex)
        {
            _logger.Warn($"No se pudo verificar el estado de los montajes: {ex.Message}");
            return true;
        }
    }

    private async Task VerifyNoMountsRemainAsync(string mountDir)
    {
        var stillMounted = await IsStillMountedAsync(mountDir).ConfigureAwait(false);
        if (stillMounted)
            _logger.Warn("La imagen sigue apareciendo como montada; revisar manualmente.");
        else
            _logger.Info("Ningún montaje abierto tras la operación.");
    }

    private async Task RecoverOrphanMountsAsync(WorkingImage image, CancellationToken cancellationToken)
    {
        ProcessRunResult info;
        try
        {
            info = await _dism.GetMountedWimInfoAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warn($"No se pudo consultar el estado de los montajes: {ex.Message}");
            return;
        }

        if (!info.Succeeded)
            return;

        // "Nuestros" montajes son los que cuelgan del mismo directorio padre que
        // el workspace de esta imagen de trabajo (normalmente
        // %LOCALAPPDATA%\MRS-Windows-Builder\workspaces, pero nunca se asume la ruta).
        var ourRoot = Path.GetDirectoryName(Path.GetFullPath(image.WorkspacePath))
            ?? InventoryWorkspace.WorkspacesRoot();

        foreach (var mountDir in MountedWimInfo.ExtractMountDirs(info.StandardOutput))
        {
            if (!MountedWimInfo.IsUnder(mountDir, ourRoot))
                continue; // nunca tocar montajes de otras aplicaciones.

            _logger.Warn($"Montaje huérfano relacionado con MRS detectado: {mountDir}. Intentando recuperar...");
            try
            {
                var recovery = await _dism.UnmountWimDiscardAsync(mountDir, cancellationToken).ConfigureAwait(false);
                _logger.Info(recovery.Succeeded
                    ? $"Montaje huérfano recuperado: {mountDir}"
                    : $"No se pudo recuperar el montaje huérfano {mountDir} (código {recovery.ExitCode}).");
            }
            catch (Exception ex)
            {
                _logger.Warn($"Error al recuperar el montaje huérfano {mountDir}: {ex.Message}");
            }
        }
    }

    private static bool HasEnoughFreeSpace(string workspacePath)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(workspacePath));
            if (string.IsNullOrEmpty(root))
                return true;

            var drive = new DriveInfo(root);
            return !drive.IsReady || drive.AvailableFreeSpace > 200L * 1024 * 1024;
        }
        catch
        {
            return true; // comprobación best-effort: si no se puede determinar, no se bloquea por ello.
        }
    }

    private static string? Trim(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();
        return trimmed.Length > 2000 ? trimmed[..2000] + " […]" : trimmed;
    }

    private static RemovalExecutionResult BuildResult(
        RemovalExecutionPhase phase, bool success, WorkingImage image,
        List<RemovalExecutionItem> executed, List<RemovalExecutionItem> failed,
        List<string> warnings, List<string> errors, bool committed, bool discarded) => new()
    {
        Success = success,
        Phase = phase,
        ActionsExecuted = executed,
        ActionsFailed = failed,
        Warnings = warnings,
        Errors = errors,
        ExitCode = failed.Count > 0 ? failed[^1].ExitCode : executed.Count > 0 ? executed[^1].ExitCode : null,
        Workspace = image.WorkspacePath,
        WorkingImage = image,
        Committed = committed,
        Discarded = discarded,
    };
}
