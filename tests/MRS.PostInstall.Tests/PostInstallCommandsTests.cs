using MRS.PostInstall.Execution;
using MRS.PostInstall.Models;
using Xunit;

namespace MRS.PostInstall.Tests;

/// <summary>
/// P18, sección 5/6: la línea de comandos real vive en un único sitio
/// (<see cref="PostInstallCommands"/>) — este test la fija para que nadie la
/// reescriba accidentalmente en otro sitio del código.
/// </summary>
public sealed class PostInstallCommandsTests
{
    [Fact]
    public void DotNetRuntimeInstall_uses_the_documented_silent_flags()
    {
        var spec = PostInstallCommands.DotNetRuntimeInstall(new PostInstallConfiguration(), @"C:\pkg\dotnet");

        Assert.Equal("/install /quiet /norestart", spec.Arguments);
        Assert.Equal(@"C:\pkg\dotnet\windowsdesktop-runtime-8.0.26-win-x64.exe", spec.Executable);
        Assert.Equal(@"C:\pkg\dotnet", spec.WorkingDirectory);
        Assert.Contains(0, spec.ExpectedExitCodes);
    }

    [Fact]
    public void Pcpi_is_launched_without_arguments_from_its_own_directory()
    {
        var spec = PostInstallCommands.Pcpi(new PostInstallConfiguration(), @"C:\pkg\pcpi");

        Assert.Equal(string.Empty, spec.Arguments);
        Assert.Equal(@"C:\pkg\pcpi\PCPI-Retro-Minimals-Portable-0.0.5.exe", spec.Executable);
        Assert.Equal(@"C:\pkg\pcpi", spec.WorkingDirectory);
    }

    [Fact]
    public void Commands_use_the_configured_file_names_not_the_defaults()
    {
        var config = new PostInstallConfiguration
        {
            DotNetInstallerFileName = "custom-runtime.exe",
            PcpiFileName = "custom-pcpi.exe",
        };

        var dotnet = PostInstallCommands.DotNetRuntimeInstall(config, @"C:\pkg\dotnet");
        var pcpi = PostInstallCommands.Pcpi(config, @"C:\pkg\pcpi");

        Assert.EndsWith("custom-runtime.exe", dotnet.Executable);
        Assert.EndsWith("custom-pcpi.exe", pcpi.Executable);
    }
}
