using MRS.ImageEngine.Models;
using MRS.RemovalEngine.Models;
using MRS.RemovalPlanning.Models;

namespace MRS.RemovalEngine;

/// <summary>
/// Compara el inventario de la imagen de trabajo ANTES y DESPUÉS de una
/// ejecución para confirmar (con datos reales, no solo con el ExitCode) que lo
/// eliminado realmente lo está.
/// </summary>
public sealed class RemovalVerifier
{
    public RemovalVerificationResult Verify(RemovalExecutionResult executionResult, ImageInventory before, ImageInventory after)
    {
        ArgumentNullException.ThrowIfNull(executionResult);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var removed = new List<string>();
        var stillPresent = new List<string>();
        var warnings = new List<string>();

        foreach (var item in executionResult.ActionsExecuted)
        {
            if (IsGone(item.ActionType, item.Target, after))
            {
                removed.Add(item.ComponentId);
            }
            else
            {
                stillPresent.Add(item.ComponentId);
                warnings.Add($"{item.ComponentId} se marcó como eliminado pero sigue apareciendo en el reinventario.");
            }
        }

        var failed = executionResult.ActionsFailed.Select(i => i.ComponentId).ToList();
        var unexpected = DetectUnexpectedChanges(executionResult, before, after);

        return new RemovalVerificationResult
        {
            Removed = removed,
            StillPresent = stillPresent,
            Failed = failed,
            UnexpectedChanges = unexpected,
            Warnings = warnings,
        };
    }

    private static bool IsGone(RemovalActionType action, string target, ImageInventory after)
    {
        switch (action)
        {
            case RemovalActionType.RemoveAppx:
                return !after.ProvisionedApps.Any(a => string.Equals(a.PackageName, target, StringComparison.OrdinalIgnoreCase));

            case RemovalActionType.RemovePackage:
                return !after.Packages.Any(p => string.Equals(p.PackageIdentity, target, StringComparison.OrdinalIgnoreCase)
                                                 && string.Equals(p.State, "Installed", StringComparison.OrdinalIgnoreCase));

            case RemovalActionType.DisableFeature:
                var feature = after.Features.FirstOrDefault(f => string.Equals(f.Name, target, StringComparison.OrdinalIgnoreCase));
                return feature is null || !string.Equals(feature.State, "Enabled", StringComparison.OrdinalIgnoreCase);

            case RemovalActionType.RemoveCapability:
                return !after.Capabilities.Any(c => string.Equals(c.Identity, target, StringComparison.OrdinalIgnoreCase)
                                                     && string.Equals(c.State, "Installed", StringComparison.OrdinalIgnoreCase));

            default:
                return true;
        }
    }

    private static IReadOnlyList<string> DetectUnexpectedChanges(
        RemovalExecutionResult result, ImageInventory before, ImageInventory after)
    {
        var changes = new List<string>();

        CompareCount("Paquetes", before.PackageCount, after.PackageCount,
            result.ActionsExecuted.Count(a => a.ActionType == RemovalActionType.RemovePackage), changes);
        CompareCount("Apps", before.ProvisionedAppCount, after.ProvisionedAppCount,
            result.ActionsExecuted.Count(a => a.ActionType == RemovalActionType.RemoveAppx), changes);
        CompareCount("Capabilities", before.CapabilityCount, after.CapabilityCount,
            result.ActionsExecuted.Count(a => a.ActionType == RemovalActionType.RemoveCapability), changes);

        return changes;
    }

    private static void CompareCount(string label, int before, int after, int expectedRemovals, List<string> changes)
    {
        var actualDelta = before - after;
        if (actualDelta != expectedRemovals)
            changes.Add($"{label}: se esperaban {expectedRemovals} menos y hay {actualDelta} menos.");
    }
}
