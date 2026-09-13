using MRS.ISOEngine.Configuration;
using Xunit;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P16, sección 9: si <c>BypassStorage</c> está activado, la validación debe
/// impedir una generación engañosa (sin mecanismo implementado), con un mensaje
/// claro. Nunca debe ignorarse la opción en silencio.
/// </summary>
public sealed class InstallationExecutionValidatorTests
{
    [Fact]
    public void A_valid_configuration_without_storage_bypass_passes()
    {
        var result = InstallationExecutionValidator.Validate(InstallationOptionsModel.Default with { BypassStorage = false });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Storage_bypass_enabled_fails_validation_with_a_clear_message()
    {
        var result = InstallationExecutionValidator.Validate(InstallationOptionsModel.Default with { BypassStorage = true });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e == "El bypass de almacenamiento no está implementado/validado para esta build.");
    }

    [Fact]
    public void Storage_bypass_disabled_does_not_block_the_other_bypasses()
    {
        var options = new InstallationOptionsModel
        {
            BypassTpm = true, BypassSecureBoot = true, BypassCpu = true, BypassRam = true, BypassStorage = false,
        };

        var result = InstallationExecutionValidator.Validate(options);

        Assert.True(result.IsValid);
    }
}
