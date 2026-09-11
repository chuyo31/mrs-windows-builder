using MRS.ComponentCatalog.Models;
using MRS.ComponentCatalog.Rules;
using Xunit;

namespace MRS.ComponentCatalog.Tests;

public class JsonCatalogRuleLoaderTests
{
    [Fact]
    public void Parses_classification_rules_from_json()
    {
        const string json = """
            [
              { "id": "gaming-xbox", "pattern": "Xbox", "category": "Gaming", "tags": ["gaming"] }
            ]
            """;

        var rules = JsonCatalogRuleLoader.ParseClassificationRules(json);

        var rule = Assert.Single(rules);
        Assert.Equal("gaming-xbox", rule.Id);
        Assert.Equal("Xbox", rule.Pattern);
        Assert.Equal(ComponentCategory.Gaming, rule.Category);
        Assert.Contains("gaming", rule.Tags);
    }

    [Fact]
    public void Parses_protection_rules_from_json()
    {
        const string json = """
            [
              { "id": "windows-servicing", "pattern": "ServicingStack", "category": "WindowsUpdate", "risk": "Critical", "reason": "Necesario para el mantenimiento de Windows" }
            ]
            """;

        var rules = JsonCatalogRuleLoader.ParseProtectionRules(json);

        var rule = Assert.Single(rules);
        Assert.Equal("windows-servicing", rule.Id);
        Assert.Equal(ComponentRisk.Critical, rule.Risk);
        Assert.Equal("Necesario para el mantenimiento de Windows", rule.Reason);
    }

    [Fact]
    public void TryLoadFromDirectory_returns_null_when_no_files_exist()
    {
        var missingDir = Path.Combine(Path.GetTempPath(), "mrs-catalog-tests", Guid.NewGuid().ToString("N"));

        var result = JsonCatalogRuleLoader.TryLoadFromDirectory(missingDir);

        Assert.Null(result);
    }

    [Fact]
    public void TryLoadFromDirectory_loads_both_files_when_present()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mrs-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "components.json"),
                """[{ "id": "r1", "pattern": "Xbox", "category": "Gaming" }]""");
            File.WriteAllText(Path.Combine(dir, "protection-rules.json"),
                """[{ "id": "r2", "pattern": "Defender", "risk": "Critical", "reason": "test" }]""");

            var result = JsonCatalogRuleLoader.TryLoadFromDirectory(dir);

            Assert.NotNull(result);
            Assert.Single(result!.ClassificationRules);
            Assert.Single(result.ProtectionRules);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
