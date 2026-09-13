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

    /// <summary>
    /// <paramref name="securityOptions"/> (P13) decide si las reglas marcadas con
    /// <see cref="ProtectionRule.SecurityFeature"/> (Defender / Windows Update) se
    /// siguen aplicando. <c>null</c> equivale a <see cref="SecurityOptions.Safe"/>:
    /// mismo comportamiento que antes de P13. El resto de reglas (sin
    /// <see cref="ProtectionRule.SecurityFeature"/>) nunca se ven afectadas.
    /// </summary>
    public ComponentDefinition Apply(ComponentDefinition component, SecurityOptions? securityOptions = null)
    {
        var options = securityOptions ?? SecurityOptions.Safe;
        var matchText = $"{component.Name} {component.DisplayName} {component.Description}";

        foreach (var rule in _rules)
        {
            if (!matchText.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsSuppressedByOptions(rule, options))
                continue; // KeepDefender/KeepWindowsUpdate = false: esta regla concreta no protege, pero no toca ninguna otra.

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

    public IReadOnlyList<ComponentDefinition> ApplyAll(IEnumerable<ComponentDefinition> components, SecurityOptions? securityOptions = null)
        => components.Select(c => Apply(c, securityOptions)).ToList();

    private static bool IsSuppressedByOptions(ProtectionRule rule, SecurityOptions options) => rule.SecurityFeature switch
    {
        SecurityFeature.Defender => !options.KeepDefender,
        SecurityFeature.WindowsUpdate => !options.KeepWindowsUpdate,
        _ => false,
    };

    private static ComponentRisk MaxRisk(ComponentRisk a, ComponentRisk b) => (ComponentRisk)Math.Max((int)a, (int)b);
}
