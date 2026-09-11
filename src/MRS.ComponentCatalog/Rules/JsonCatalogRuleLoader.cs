using System.Text.Json;
using System.Text.Json.Serialization;

namespace MRS.ComponentCatalog.Rules;

/// <summary>
/// Carga reglas externas desde <c>catalog/&lt;os&gt;/components.json</c> y
/// <c>catalog/&lt;os&gt;/protection-rules.json</c>, para poder actualizar las
/// reglas sin recompilar el motor. Si los archivos no existen o no son válidos,
/// el llamador debe usar <see cref="DefaultCatalogRules"/> como respaldo.
/// </summary>
public static class JsonCatalogRuleLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Carga <c>components.json</c> y <c>protection-rules.json</c> desde <paramref name="osFolder"/>.</summary>
    public static CatalogRuleSet? TryLoadFromDirectory(string osFolder)
    {
        var componentsPath = Path.Combine(osFolder, "components.json");
        var protectionPath = Path.Combine(osFolder, "protection-rules.json");

        var classification = TryLoadClassificationRules(componentsPath);
        var protection = TryLoadProtectionRules(protectionPath);

        if (classification is null && protection is null)
            return null;

        return new CatalogRuleSet(
            classification ?? Array.Empty<ClassificationRule>(),
            protection ?? Array.Empty<ProtectionRule>());
    }

    public static IReadOnlyList<ClassificationRule>? TryLoadClassificationRules(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return ParseClassificationRules(json);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    public static IReadOnlyList<ProtectionRule>? TryLoadProtectionRules(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return ParseProtectionRules(json);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>Parsea un JSON de reglas de clasificación ya en memoria (usado también por los tests).</summary>
    public static IReadOnlyList<ClassificationRule> ParseClassificationRules(string json)
        => JsonSerializer.Deserialize<List<ClassificationRule>>(json, Options)
           ?? new List<ClassificationRule>();

    /// <summary>Parsea un JSON de reglas de protección ya en memoria (usado también por los tests).</summary>
    public static IReadOnlyList<ProtectionRule> ParseProtectionRules(string json)
        => JsonSerializer.Deserialize<List<ProtectionRule>>(json, Options)
           ?? new List<ProtectionRule>();
}
