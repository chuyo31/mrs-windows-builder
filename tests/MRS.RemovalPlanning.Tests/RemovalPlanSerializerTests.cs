using MRS.ComponentCatalog.Models;
using MRS.ImageEngine.Models;
using MRS.RemovalPlanning.Models;
using MRS.RemovalPlanning.Serialization;
using Xunit;

namespace MRS.RemovalPlanning.Tests;

public class RemovalPlanSerializerTests
{
    private readonly RemovalPlanBuilder _builder = new();
    private static readonly ImageInventory EmptyInventory = new();

    private RemovalPlan SamplePlan()
    {
        var clipchamp = TestComponents.Appx("clipchamp", "Clipchamp");
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00", protection: ComponentProtection.Protected,
            category: ComponentCategory.Framework, protectionReason: "Framework compartido.");
        var catalog = TestComponents.Catalog(new[] { clipchamp, vclibs });

        return _builder.Build(EmptyInventory, catalog,
            new[]
            {
                new ComponentSelection(clipchamp.Id, true),
                new ComponentSelection(vclibs.Id, true),
            },
            imageId: "Windows 11 Pro (índice 2)");
    }

    [Fact]
    public void Plan_round_trips_through_json()
    {
        var plan = SamplePlan();

        var json = RemovalPlanSerializer.ToJson(plan);
        var restored = RemovalPlanSerializer.FromJson(json);

        Assert.Equal(plan.ImageId, restored.ImageId);
        Assert.Equal(plan.Components.Count, restored.Components.Count);
        Assert.Equal(plan.TotalSelected, restored.TotalSelected);
        Assert.Equal(plan.TotalAllowed, restored.TotalAllowed);
        Assert.Equal(plan.TotalBlocked, restored.TotalBlocked);
    }

    [Fact]
    public void Json_contains_the_format_version()
    {
        var json = RemovalPlanSerializer.ToJson(SamplePlan());

        Assert.Contains("formatVersion", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(RemovalPlanSerializer.CurrentFormatVersion.ToString(), json);
    }

    [Fact]
    public void Json_contains_image_information_and_date()
    {
        var json = RemovalPlanSerializer.ToJson(SamplePlan());

        Assert.Contains("Windows 11 Pro", json);
        Assert.Contains("createdAt", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Actions_survive_the_round_trip()
    {
        var plan = SamplePlan();
        var restored = RemovalPlanSerializer.FromJson(RemovalPlanSerializer.ToJson(plan));

        Assert.Equal(plan.Actions.Count, restored.Actions.Count);
        Assert.Equal(plan.Actions[0].ActionType, restored.Actions[0].ActionType);
        Assert.Equal(plan.Actions[0].ComponentId, restored.Actions[0].ComponentId);
    }

    [Fact]
    public void Warnings_survive_the_round_trip()
    {
        var plan = SamplePlan();
        var restored = RemovalPlanSerializer.FromJson(RemovalPlanSerializer.ToJson(plan));

        Assert.Equal(plan.Warnings.Count, restored.Warnings.Count);
        Assert.Contains(restored.Warnings, w => w.Severity == RemovalWarningSeverity.Critical);
    }

    [Fact]
    public void Blocked_components_survive_the_round_trip()
    {
        var plan = SamplePlan();
        var restored = RemovalPlanSerializer.FromJson(RemovalPlanSerializer.ToJson(plan));

        var blocked = restored.Components.Single(c => !c.Allowed);
        Assert.NotNull(blocked.BlockReason);
        Assert.Equal(plan.TotalBlocked, restored.TotalBlocked);
    }

    [Fact]
    public void SaveToFile_and_LoadFromFile_round_trip_and_create_directories()
    {
        var plan = SamplePlan();
        var path = Path.Combine(Path.GetTempPath(), "mrs-removalplan-tests", Guid.NewGuid().ToString("N"), "output", "removal-plan.json");

        try
        {
            RemovalPlanSerializer.SaveToFile(plan, path);
            Assert.True(File.Exists(path));

            var restored = RemovalPlanSerializer.LoadFromFile(path);
            Assert.Equal(plan.TotalSelected, restored.TotalSelected);
        }
        finally
        {
            var root = Path.GetDirectoryName(Path.GetDirectoryName(path))!;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
