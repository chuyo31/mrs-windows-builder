using MRS.ImageEngine.Models;
using MRS.RemovalEngine.Models;
using MRS.RemovalPlanning.Models;
using Xunit;

namespace MRS.RemovalEngine.Tests;

public class RemovalVerifierTests
{
    private readonly RemovalVerifier _verifier = new();

    private static RemovalExecutionItem Executed(string componentId, RemovalActionType action, string target) => new()
    {
        ComponentId = componentId, ActionType = action, Target = target, Success = true,
        StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow, ExitCode = 0,
    };

    [Fact]
    public void Appx_removed_when_it_no_longer_appears_in_provisioned_apps()
    {
        var executed = Executed("appx:Clipchamp", RemovalActionType.RemoveAppx, "Clipchamp.PackageName");
        var result = new RemovalExecutionResult { ActionsExecuted = new[] { executed } };

        var before = new ImageInventory { ProvisionedApps = new[] { new ProvisionedApp { PackageName = "Clipchamp.PackageName" } } };
        var after = new ImageInventory();

        var verification = _verifier.Verify(result, before, after);

        Assert.Contains("appx:Clipchamp", verification.Removed);
        Assert.Empty(verification.StillPresent);
    }

    [Fact]
    public void Appx_still_present_is_flagged_with_a_warning()
    {
        var executed = Executed("appx:Clipchamp", RemovalActionType.RemoveAppx, "Clipchamp.PackageName");
        var result = new RemovalExecutionResult { ActionsExecuted = new[] { executed } };

        var after = new ImageInventory { ProvisionedApps = new[] { new ProvisionedApp { PackageName = "Clipchamp.PackageName" } } };

        var verification = _verifier.Verify(result, new ImageInventory(), after);

        Assert.Contains("appx:Clipchamp", verification.StillPresent);
        Assert.NotEmpty(verification.Warnings);
    }

    [Fact]
    public void Feature_removed_when_no_longer_enabled()
    {
        var executed = Executed("feature:MediaPlayback", RemovalActionType.DisableFeature, "MediaPlayback");
        var result = new RemovalExecutionResult { ActionsExecuted = new[] { executed } };

        var after = new ImageInventory { Features = new[] { new ImageFeature { Name = "MediaPlayback", State = "Disabled" } } };

        var verification = _verifier.Verify(result, new ImageInventory(), after);

        Assert.Contains("feature:MediaPlayback", verification.Removed);
    }

    [Fact]
    public void Feature_still_present_when_still_enabled()
    {
        var executed = Executed("feature:MediaPlayback", RemovalActionType.DisableFeature, "MediaPlayback");
        var result = new RemovalExecutionResult { ActionsExecuted = new[] { executed } };

        var after = new ImageInventory { Features = new[] { new ImageFeature { Name = "MediaPlayback", State = "Enabled" } } };

        var verification = _verifier.Verify(result, new ImageInventory(), after);

        Assert.Contains("feature:MediaPlayback", verification.StillPresent);
    }

    [Fact]
    public void Package_removed_when_no_longer_installed()
    {
        var executed = Executed("package:Contoso", RemovalActionType.RemovePackage, "Contoso.Identity");
        var result = new RemovalExecutionResult { ActionsExecuted = new[] { executed } };

        var verification = _verifier.Verify(result, new ImageInventory(), new ImageInventory());

        Assert.Contains("package:Contoso", verification.Removed);
    }

    [Fact]
    public void Capability_removed_when_no_longer_installed()
    {
        var executed = Executed("capability:OpenSSH", RemovalActionType.RemoveCapability, "OpenSSH.Client~~~~0.0.1.0");
        var result = new RemovalExecutionResult { ActionsExecuted = new[] { executed } };

        var verification = _verifier.Verify(result, new ImageInventory(), new ImageInventory());

        Assert.Contains("capability:OpenSSH", verification.Removed);
    }

    [Fact]
    public void Failed_actions_are_reported_as_failed()
    {
        var failedItem = new RemovalExecutionItem { ComponentId = "appx:Bad", ActionType = RemovalActionType.RemoveAppx, Target = "Bad", Success = false };
        var result = new RemovalExecutionResult { ActionsFailed = new[] { failedItem } };

        var verification = _verifier.Verify(result, new ImageInventory(), new ImageInventory());

        Assert.Contains("appx:Bad", verification.Failed);
    }

    [Fact]
    public void No_unexpected_changes_when_counts_match_expectations()
    {
        var executed = Executed("appx:Clipchamp", RemovalActionType.RemoveAppx, "Clipchamp.PackageName");
        var result = new RemovalExecutionResult { ActionsExecuted = new[] { executed } };

        var before = new ImageInventory { ProvisionedApps = new[] { new ProvisionedApp { PackageName = "Clipchamp.PackageName" }, new ProvisionedApp { PackageName = "Other" } } };
        var after = new ImageInventory { ProvisionedApps = new[] { new ProvisionedApp { PackageName = "Other" } } };

        var verification = _verifier.Verify(result, before, after);

        Assert.Empty(verification.UnexpectedChanges);
    }

    [Fact]
    public void Unexpected_changes_are_reported_when_counts_do_not_match()
    {
        var executed = Executed("appx:Clipchamp", RemovalActionType.RemoveAppx, "Clipchamp.PackageName");
        var result = new RemovalExecutionResult { ActionsExecuted = new[] { executed } };

        // Se esperaba 1 app menos, pero desaparecieron 2: cambio inesperado.
        var before = new ImageInventory { ProvisionedApps = new[] { new ProvisionedApp { PackageName = "A" }, new ProvisionedApp { PackageName = "B" } } };
        var after = new ImageInventory();

        var verification = _verifier.Verify(result, before, after);

        Assert.NotEmpty(verification.UnexpectedChanges);
    }
}
