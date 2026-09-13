using MRS.ISOEngine.Configuration;
using MRS.ISOEngine.Models;
using Xunit;
// Mismo caso que MRS.RemovalEngine/MRS.ProfileEngine/MRS.InstallationOptions: dentro del
// árbol "MRS.*" el namespace MRS.InstallationOptions gana sobre el tipo del mismo nombre
// en la búsqueda de identificadores sin cualificar, así que se usa un alias con un nombre
// distinto.
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P15: <see cref="InstallationConfigurationPlanner"/> es una función pura -
/// InstallationOptions in, lista de acciones out - sin tocar ningún archivo ni
/// ejecutar DISM. No depende de <see cref="GenerationWorkspace"/> ni de ningún
/// estado externo: el mismo input siempre produce el mismo output.
/// </summary>
public sealed class InstallationConfigurationPlannerTests
{
    [Fact]
    public void Default_options_produce_one_action_per_option_in_the_documented_order()
    {
        var actions = InstallationConfigurationPlanner.Plan(InstallationOptionsModel.Default);

        var logLines = actions.Select(a => a.ToLogLine()).ToList();

        Assert.Equal(new[]
        {
            "[INSTALL] Local account enabled",
            "[INSTALL] Offline OOBE enabled",
            "[COMPAT] TPM bypass enabled",
            "[COMPAT] Secure Boot bypass enabled",
            "[COMPAT] CPU bypass enabled",
            "[COMPAT] RAM bypass enabled",
            "[COMPAT] Storage bypass enabled",
        }, logLines);
    }

    [Fact]
    public void Disabling_an_option_removes_only_its_own_action()
    {
        var options = InstallationOptionsModel.Default with { BypassTpm = false };

        var actions = InstallationConfigurationPlanner.Plan(options);

        Assert.DoesNotContain(actions, a => a.Description == "TPM bypass enabled");
        Assert.Equal(6, actions.Count); // las otras 6 siguen presentes
    }

    [Fact]
    public void Disabling_everything_produces_an_empty_plan_never_an_invalid_one()
    {
        var options = new InstallationOptionsModel
        {
            AllowLocalAccount = false,
            AllowOfflineOobe = false,
            BypassTpm = false,
            BypassSecureBoot = false,
            BypassCpu = false,
            BypassRam = false,
            BypassStorage = false,
        };

        var actions = InstallationConfigurationPlanner.Plan(options);

        Assert.Empty(actions);
    }

    [Fact]
    public void Install_actions_always_come_before_compat_actions()
    {
        var actions = InstallationConfigurationPlanner.Plan(InstallationOptionsModel.Default);

        var lastInstallIndex = actions.ToList().FindLastIndex(a => a.Category == InstallationActionCategory.Install);
        var firstCompatIndex = actions.ToList().FindIndex(a => a.Category == InstallationActionCategory.Compat);

        Assert.True(lastInstallIndex < firstCompatIndex);
    }

    [Fact]
    public void Planning_is_deterministic_for_the_same_input()
    {
        var options = InstallationOptionsModel.Default with { BypassRam = false };

        var first = InstallationConfigurationPlanner.Plan(options).Select(a => a.ToLogLine()).ToList();
        var second = InstallationConfigurationPlanner.Plan(options).Select(a => a.ToLogLine()).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Every_action_declares_a_mechanism_and_a_target_artifact()
    {
        var actions = InstallationConfigurationPlanner.Plan(InstallationOptionsModel.Default);

        Assert.All(actions, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Mechanism));
            Assert.False(string.IsNullOrWhiteSpace(a.TargetArtifact));
        });
    }

    [Fact]
    public void The_planner_never_touches_the_filesystem()
    {
        // Función pura: no acepta ningún workspace ni ruta, solo InstallationOptions.
        // Este test documenta esa garantía comprobando la forma del método.
        var method = typeof(InstallationConfigurationPlanner).GetMethod(nameof(InstallationConfigurationPlanner.Plan));

        Assert.NotNull(method);
        Assert.Single(method!.GetParameters());
        Assert.Equal(typeof(InstallationOptionsModel), method.GetParameters()[0].ParameterType);
    }
}
