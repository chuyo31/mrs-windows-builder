using MRS.ComponentCatalog.Models;
using MRS.ComponentCatalog.Rules;

namespace MRS.ComponentCatalog.Classification;

/// <summary>
/// Evita que un usuario pueda marcar accidentalmente como eliminable un
/// componente crítico. Se aplica DESPUÉS de <see cref="CatalogClassifier"/> y
/// puede reforzar (nunca debilitar) la protección/riesgo de un componente ya
/// clasificado. Las reglas son específicas y revisables (Parte 8): nunca "todo
/// Microsoft-Windows es protegido".
/// </summary>
public sealed class ProtectionEngine
{
    private readonly IReadOnlyList<ProtectionRule> _rules;

    public ProtectionEngine(CatalogRuleSet? ruleSet = null)
        => _rules = (ruleSet ?? DefaultCatalogRules.Windows11).ProtectionRules
            .Where(r => r.Enabled)
            .ToList();

    public ComponentDefinition Apply(ComponentDefinition component)
    {
        var matchText = $"{component.Name} {component.DisplayName} {component.Description}";

        foreach (var rule in _rules)
        {
            if (!matchText.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase))
                continue;

            return component with
            {
                Protection = ComponentProtection.Protected,
                Risk = MaxRisk(component.Risk, rule.Risk),
                ProtectionReason = rule.Reason,
                Category = component.Category == ComponentCategory.Unknown && rule.Category is { } category
                    ? category
                    : component.Category,
            };
        }

        return component;
    }

    public IReadOnlyList<ComponentDefinition> ApplyAll(IEnumerable<ComponentDefinition> components)
        => components.Select(Apply).ToList();

    private static ComponentRisk MaxRisk(ComponentRisk a, ComponentRisk b) => (ComponentRisk)Math.Max((int)a, (int)b);
}
