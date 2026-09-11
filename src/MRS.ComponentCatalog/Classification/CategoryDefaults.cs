using MRS.ComponentCatalog.Models;

namespace MRS.ComponentCatalog.Classification;

/// <summary>
/// Riesgo/protección de referencia por categoría, aplicados ANTES de las
/// reglas de protección específicas. Deliberadamente conservadores: ninguna
/// categoría se marca "Protected" solo por pertenecer a ella (Parte 8); eso
/// requiere una <see cref="Rules.ProtectionRule"/> explícita.
/// </summary>
internal static class CategoryDefaults
{
    private static readonly Dictionary<ComponentCategory, (ComponentRisk Risk, ComponentProtection Protection)> Map = new()
    {
        [ComponentCategory.System] = (ComponentRisk.High, ComponentProtection.Recommended),
        [ComponentCategory.Security] = (ComponentRisk.Critical, ComponentProtection.Recommended),
        [ComponentCategory.WindowsUpdate] = (ComponentRisk.Critical, ComponentProtection.Recommended),
        [ComponentCategory.Store] = (ComponentRisk.Medium, ComponentProtection.Optional),
        [ComponentCategory.Application] = (ComponentRisk.Low, ComponentProtection.Removable),
        [ComponentCategory.Gaming] = (ComponentRisk.Low, ComponentProtection.Removable),
        [ComponentCategory.Communication] = (ComponentRisk.Low, ComponentProtection.Removable),
        [ComponentCategory.AI] = (ComponentRisk.Low, ComponentProtection.Removable),
        [ComponentCategory.Telemetry] = (ComponentRisk.Low, ComponentProtection.Removable),
        [ComponentCategory.Media] = (ComponentRisk.Medium, ComponentProtection.Optional),
        [ComponentCategory.Networking] = (ComponentRisk.High, ComponentProtection.Recommended),
        [ComponentCategory.Printing] = (ComponentRisk.Medium, ComponentProtection.Optional),
        [ComponentCategory.Accessibility] = (ComponentRisk.Medium, ComponentProtection.Recommended),
        [ComponentCategory.Development] = (ComponentRisk.Low, ComponentProtection.Optional),
        [ComponentCategory.Language] = (ComponentRisk.Medium, ComponentProtection.Recommended),
        [ComponentCategory.Driver] = (ComponentRisk.Medium, ComponentProtection.Recommended),
        [ComponentCategory.Feature] = (ComponentRisk.Medium, ComponentProtection.Unknown),
        [ComponentCategory.Capability] = (ComponentRisk.Medium, ComponentProtection.Unknown),
        [ComponentCategory.Framework] = (ComponentRisk.High, ComponentProtection.Recommended),
        [ComponentCategory.Unknown] = (ComponentRisk.Unknown, ComponentProtection.Unknown),
    };

    public static (ComponentRisk Risk, ComponentProtection Protection) For(ComponentCategory category)
        => Map.TryGetValue(category, out var value) ? value : (ComponentRisk.Unknown, ComponentProtection.Unknown);
}
