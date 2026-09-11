using MRS.ComponentCatalog.Models;
using MRS.RemovalPlanning.Models;

namespace MRS.RemovalPlanning;

/// <summary>
/// Segunda capa de verificación, independiente de <see cref="RemovalPlanBuilder"/>:
/// vuelve a comprobar un plan ya construido contra el catálogo antes de que
/// pudiera entregarse al futuro <c>RemovalEngine</c>. Tampoco referencia DISM.
/// </summary>
public sealed class RemovalPlanValidator
{
    public PlanValidationResult Validate(RemovalPlan plan, ComponentCatalogResult catalog)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(catalog);

        var errors = new List<string>();
        var warnings = new List<RemovalWarning>(plan.Warnings);

        var componentsById = catalog.Components.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var allowedIds = plan.Components.Where(i => i.Allowed).Select(i => i.ComponentId).ToHashSet(StringComparer.Ordinal);

        foreach (var item in plan.Components)
        {
            if (string.IsNullOrWhiteSpace(item.ComponentId))
            {
                errors.Add("Elemento del plan sin ComponentId.");
                continue;
            }

            if (!componentsById.TryGetValue(item.ComponentId, out var component))
            {
                errors.Add($"El componente '{item.ComponentId}' no existe en el catálogo.");
                continue;
            }

            if (!component.Detected)
                errors.Add($"El componente '{item.ComponentId}' no está detectado en el inventario.");

            if (item.Allowed && component.Protection == ComponentProtection.Protected)
                errors.Add($"El componente '{item.ComponentId}' está protegido y no puede tener una acción permitida.");

            if (item.Allowed && !IsActionCompatible(item.Action, component.SourceType))
                errors.Add($"La acción '{item.Action}' no es compatible con el origen '{component.SourceType}' de '{item.ComponentId}'.");

            if (item.Allowed && item.CurrentState is ComponentCurrentState.Unknown or ComponentCurrentState.NotPresent)
                errors.Add($"El componente '{item.ComponentId}' tiene un estado ({item.CurrentState}) que no permite una acción.");

            if (!item.Allowed && item.Action != RemovalActionType.None)
                errors.Add($"El componente '{item.ComponentId}' tiene una acción distinta de None pese a no estar permitido.");

            if (item.Allowed)
            {
                var conflicting = component.Conflicts.FirstOrDefault(allowedIds.Contains);
                if (conflicting is not null)
                    errors.Add($"El componente '{item.ComponentId}' está en conflicto con '{conflicting}', también permitido en el plan.");

                var remainingDependent = catalog.DependentsOf(item.ComponentId)
                    .Select(d => d.Id)
                    .FirstOrDefault(depId => !allowedIds.Contains(depId));
                if (remainingDependent is not null)
                    errors.Add($"El componente '{item.ComponentId}' es requerido por '{remainingDependent}', que permanecerá en la imagen.");
            }
        }

        return new PlanValidationResult(errors.Count == 0, errors, warnings);
    }

    private static bool IsActionCompatible(RemovalActionType action, ComponentSourceType sourceType) => (action, sourceType) switch
    {
        (RemovalActionType.None, _) => true,
        (RemovalActionType.RemovePackage, ComponentSourceType.Package) => true,
        (RemovalActionType.RemoveAppx, ComponentSourceType.Appx) => true,
        (RemovalActionType.DisableFeature, ComponentSourceType.Feature) => true,
        (RemovalActionType.RemoveCapability, ComponentSourceType.Capability) => true,
        _ => false,
    };
}
