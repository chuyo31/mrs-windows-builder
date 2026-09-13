using MRS.PostInstall.Configuration;
using MRS.PostInstall.Models;
using Xunit;

namespace MRS.PostInstall.Tests;

/// <summary>P18, sección 18 (Configuration): valores por defecto, deshabilitar, versión 8.0.26, x64, configuración inválida.</summary>
public sealed class PostInstallConfigurationValidatorTests
{
    [Fact]
    public void Default_configuration_is_valid()
    {
        var result = PostInstallConfigurationValidator.Validate(new PostInstallConfiguration());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Default_values_match_the_prompt_target()
    {
        var config = new PostInstallConfiguration();

        Assert.True(config.Enabled);
        Assert.Equal("8.0.26", config.DotNetRuntimeVersion);
        Assert.Equal("x64", config.Architecture);
        Assert.Equal("windowsdesktop-runtime-8.0.26-win-x64.exe", config.DotNetInstallerFileName);
        Assert.Equal("PCPI-Retro-Minimals-Portable-0.0.5.exe", config.PcpiFileName);
        Assert.True(config.RunPcpiAfterRuntime);
    }

    [Fact]
    public void Disabled_configuration_is_always_valid_even_with_nonsense_values()
    {
        var config = new PostInstallConfiguration { Enabled = false, Architecture = "arm64", DotNetRuntimeVersion = "no-version" };

        var result = PostInstallConfigurationValidator.Validate(config);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("8.0.26")]
    [InlineData("9.0.0")]
    public void Well_formed_version_numbers_are_accepted(string version)
    {
        var result = PostInstallConfigurationValidator.Validate(new PostInstallConfiguration { DotNetRuntimeVersion = version });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("8.0")]
    [InlineData("latest")]
    public void Malformed_version_numbers_are_rejected(string version)
    {
        var result = PostInstallConfigurationValidator.Validate(new PostInstallConfiguration { DotNetRuntimeVersion = version });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Only_x64_is_accepted_as_architecture()
    {
        var result = PostInstallConfigurationValidator.Validate(new PostInstallConfiguration { Architecture = "arm64" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("x64"));
    }

    [Theory]
    [InlineData(@"C:\Users\Dev\runtime.exe")]
    [InlineData(@"..\runtime.exe")]
    [InlineData("sub/runtime.exe")]
    public void A_file_name_that_looks_like_a_path_is_rejected(string fileName)
    {
        var result = PostInstallConfigurationValidator.Validate(new PostInstallConfiguration { DotNetInstallerFileName = fileName });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Identical_file_names_for_dotnet_and_pcpi_are_rejected()
    {
        var config = new PostInstallConfiguration { DotNetInstallerFileName = "same.exe", PcpiFileName = "same.exe" };

        var result = PostInstallConfigurationValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("coincidir"));
    }
}
