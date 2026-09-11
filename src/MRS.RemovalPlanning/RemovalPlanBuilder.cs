using MRS.ComponentCatalog.Models;
using MRS.ImageEngine.Models;
using MRS.RemovalPlanning.Models;

namespace MRS.RemovalPlanning;

/// <summary>
///   Selección  ->  Catálogo  ->  Protección  ->  Dependencias  ->  Validación  ->  RemovalPlan
///
/// Nunca ejecuta una acción solo porque el usuario marcó un checkbox: cada
/// componente pasa por protección, estado DISM y dependientes antes de
/// decidir si <see cref="RemovalPlanItem.Allowed"/> es <c>true</c>.
/// </summary>
public sealed class RemovalPlanBuilder : IRemovalPlanBuilder
{
    public RemovalPlan Build(
        ImageInventory inventory,
        ComponentCatalogResult catalog,
        IReadOnlyCollection<ComponentSelection> selections,
        string? imageId = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(selections);

        // El inventario en sí ya está resumido en `catalog` (que se construyó a
        // partir de él); se recibe explícitamente para dejar la firma preparada
        // para futuras comprobaciones cruzadas con el inventario original.
        var componentsById = catalog.Components.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var errors = new List<string>();

        // Solo lo explícitamente marcado; ids duplicados se colapsan.
        var requestedIds = selections
            .Where(s => s.Selected && !string.IsNullOrWhiteSpace(s.ComponentId))
            .Select(s => s.ComponentId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var items = new Dictionary<string, RemovalPlanItem>(StringComparer.Ordinal);

        foreach (var id in requestedIds)
        {
            if (!componentsById.TryGetValue(id, out var component))
            {
                errors.Add($"Componente no encontrado en el catálogo: '{id}'.");
                items[id] = UnknownComponentItem(id);
                continue;
            }

            items[id] = Evaluate(component, catalog);
        }

        ResolveDependentBlocks(items, catalog);
        AddSharedDependencyWarnings(items, catalog);

        return new RemovalPlan
        {
            ImageId = imageId ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            Components = items.Values.ToList(),
            Actions = BuildActions(items.Values),
            Warnings = items.Values.SelectMany(i => i.Warnings).ToList(),
            Errors = errors,
        };
    }

    // ---- Evaluación individual (protección -> estado -> tipo de origen) ---

    private static RemovalPlanItem Evaluate(ComponentDefinition component, ComponentCatalogResult catalog)
    {
        var state = ResolveState(component);

        var baseline = new RemovalPlanItem
        {
            ComponentId = component.Id,
            DisplayName = component.DisplayName.Length > 0 ? component.DisplayName : component.Name,
            Category = component.Category,
            SourceType = component.SourceType,
            CurrentState = state,
            Requested = true,
            Risk = component.Risk,
            Protection = component.Protection,
            Dependencies = catalog.DependenciesOf(component.Id).Select(d => d.Id).ToList(),
            Dependents = catalog.DependentsOf(component.Id).Select(d => d.Id).ToList(),
            EstimatedSize = EstimatedSize.Unknown,
        };

        // 1) Protección: bloqueo absoluto, siempre gana sobre lo demás.
        if (component.Protection == ComponentProtection.Protected)
        {
            return Block(baseline,
                component.ProtectionReason ?? "Componente protegido.",
                Warn("protected", "El componente está protegido y no puede eliminarse.",
                    RemovalWarningSeverity.Critical, component.Id));
        }

        // 2) Estado DISM: Superseded/Not Present/Unknown nunca generan acción en esta fase.
        switch (state)
        {
            case ComponentCurrentState.NotPresent:
                return Block(baseline, null,
                    Warn("not-present", "El componente no está presente en la imagen; no genera ninguna acción.",
                        RemovalWarningSeverity.Info, component.Id));

            case ComponentCurrentState.Superseded:
                return Block(baseline,
                    "Los componentes reemplazados (Superseded) no se eliminan en esta fase; se trata en servicing.",
                    Warn("superseded", "El componente ha sido reemplazado (Superseded); no se elimina en esta fase.",
                        RemovalWarningSeverity.Info, component.Id));

            case ComponentCurrentState.Unknown:
                return Block(baseline,
                    "Estado desconocido: no se puede evaluar de forma segura.",
                    Warn("unknown-state", "Estado desconocido para este componente.",
                        RemovalWarningSeverity.Warning, component.Id));
        }

        // 3) Acción según el origen (solo se llega aquí Installed y no protegido).
        return component.SourceType switch
        {
            ComponentSourceType.Package => Allow(baseline, RemovalActionType.RemovePackage),

            ComponentSourceType.Appx => component.Protection == ComponentProtection.Removable
                ? Allow(baseline, RemovalActionType.RemoveAppx)
                : Block(baseline, "Solo se permite eliminar aplicaciones marcadas como Removable en esta fase.",
                    Warn("appx-not-removable", "La aplicación no está marcada como Removable.",
                        RemovalWarningSeverity.Warning, component.Id)),

            ComponentSourceType.Feature => IsOptionalOrRemovable(component.Protection)
                ? Allow(baseline, RemovalActionType.DisableFeature)
                : Block(baseline, "Solo se permite deshabilitar features Optional o Removable en esta fase.",
                    Warn("feature-not-removable", "La feature no está marcada como Optional/Removable.",
                        RemovalWarningSeverity.Warning, component.Id)),

            ComponentSourceType.Capability => IsOptionalOrRemovable(component.Protection)
                ? Allow(baseline, RemovalActionType.RemoveCapability)
                : Block(baseline, "Solo se permite eliminar capabilities Optional o Removable en esta fase.",
                    Warn("capability-not-removable", "La capability no está marcada como Optional/Removable.",
                        RemovalWarningSeverity.Warning, component.Id)),

            ComponentSourceType.Driver =>
                Block(baseline, "La eliminación de drivers no está implementada en esta fase.", null),

            _ => Block(baseline, "Tipo de origen no soportado.", null),
        };
    }

    private static bool IsOptionalOrRemovable(ComponentProtection protection)
        => protection is ComponentProtection.Optional or ComponentProtection.Removable;

    private static ComponentCurrentState ResolveState(ComponentDefinition component)
    {
        if (component.Superseded)
            return ComponentCurrentState.Superseded;

        if (component.Installed)
            return ComponentCurrentState.Installed;

        // Sin "Installed": para features/capabilities equivale a "no presente"
        // (Disabled / Not Present); para el resto no podemos afirmarlo con
        // seguridad, así que se trata como desconocido y se bloquea.
        return component.SourceType is ComponentSourceType.Feature or ComponentSourceType.Capability
            ? ComponentCurrentState.NotPresent
            : ComponentCurrentState.Unknown;
    }

    // ---- Dependientes: nunca eliminar lo que otros componentes que se quedan necesitan ---

    private static void ResolveDependentBlocks(Dictionary<string, RemovalPlanItem> items, ComponentCatalogResult catalog)
    {
        bool changed;
        do
        {
            changed = false;
            var removedSet = items.Values
                .Where(i => i.Requested && i.Allowed)
                .Select(i => i.ComponentId)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var id in items.Keys.ToList())
            {
                var item = items[id];
                if (!item.Allowed)
                    continue;

                var remains = catalog.DependentsOf(id).Select(d => d.Id)
                    .Any(dependentId => !removedSet.Contains(dependentId));
                if (!remains)
                    continue;

                items[id] = Block(item,
                    "El componente es requerido por otros componentes que permanecerán en la imagen.",
                    Warn("required-by-remaining",
                        "El componente es requerido por otros componentes que permanecerán en la imagen.",
                        RemovalWarningSeverity.Critical, id));
                changed = true;
            }
        } while (changed);
    }

    private static void AddSharedDependencyWarnings(Dictionary<string, RemovalPlanItem> items, ComponentCatalogResult catalog)
    {
        foreach (var id in items.Keys.ToList())
        {
            var item = items[id];
            if (!item.Allowed)
                continue;

            var extra = item.Dependencies
                .Select(depId => catalog.Components.FirstOrDefault(c => c.Id == depId))
                .Where(dep => dep is { Protection: ComponentProtection.Protected })
                .Select(dep => Warn("uses-protected-dependency",
                    $"{item.DisplayName} utiliza {dep!.DisplayName}, que está protegido.",
                    RemovalWarningSeverity.Warning, id))
                .ToList();

            if (extra.Count > 0)
                items[id] = item with { Warnings = item.Warnings.Concat(extra).ToList() };
        }
    }

    // ---- Helpers ------------------------------------------------------------

    private static RemovalPlanItem Allow(RemovalPlanItem baseline, RemovalActionType action)
        => baseline with { Allowed = true, Action = action, BlockReason = null };

    private static RemovalPlanItem Block(RemovalPlanItem baseline, string? reason, RemovalWarning? warning)
        => baseline with
        {
            Allowed = false,
            Action = RemovalActionType.None,
            BlockReason = reason,
            Warnings = warning is null ? baseline.Warnings : baseline.Warnings.Append(warning).ToList(),
        };

    private static RemovalWarning Warn(string code, string message, RemovalWarningSeverity severity, string? componentId)
        => new(code, message, severity, componentId);

    private static RemovalPlanItem UnknownComponentItem(string id) => new()
    {
        ComponentId = id,
        DisplayName = id,
        Requested = true,
        Allowed = false,
        Action = RemovalActionType.None,
        CurrentState = ComponentCurrentState.Unknown,
        BlockReason = "Componente no encontrado en el catálogo.",
        Warnings = new[]
        {
            new RemovalWarning("component-not-found", "El componente seleccionado no existe en el catálogo.",
                RemovalWarningSeverity.Critical, id),
        },
    };

    private static IReadOnlyList<RemovalAction> BuildActions(IEnumerable<RemovalPlanItem> items)
        => items
            .Where(i => i.Allowed && i.Action != RemovalActionType.None)
            .Select(i => new RemovalAction
            {
                ActionType = i.Action,
                Target = ExtractTarget(i.ComponentId),
                ComponentId = i.ComponentId,
                EstimatedSize = i.EstimatedSize,
                Risk = i.Risk,
            })
            .ToList();

    /// <summary>Los ids del catálogo tienen forma "origen:nombre técnico"; el target es el nombre técnico.</summary>
    private static string ExtractTarget(string componentId)
    {
        var separatorIndex = componentId.IndexOf(':');
        return separatorIndex >= 0 && separatorIndex < componentId.Length - 1
            ? componentId[(separatorIndex + 1)..]
            : componentId;
    }
}
