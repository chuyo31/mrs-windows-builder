using MRS.InstallationOptions.Serialization;
using Xunit;
// El namespace de este archivo (MRS.InstallationOptions.Tests) está anidado bajo
// MRS.InstallationOptions, que también es el namespace del tipo InstallationOptions
// (MRS.InstallationOptions.Models.InstallationOptions); dentro del árbol "MRS.*" el
// namespace siempre gana en la búsqueda de nombres sin cualificar (mismo caso que
// MRS.RemovalEngine/MRS.ProfileEngine). Un alias con el MISMO nombre que el tipo
// tampoco basta aquí (el namespace sigue ganando en posiciones de tipo/atributo),
// así que se usa un alias con un nombre distinto (InstallationOptionsModel).
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.InstallationOptions.Tests;

/// <summary>
/// P15: <see cref="InstallationOptionsModel"/> es una capacidad propia de la ISO
/// (comportamiento del instalador/OOBE), deliberadamente independiente de
/// <c>MRS.ComponentCatalog.Models.SecurityOptions</c> (Defender/Windows Update,
/// P13) y de <c>ProfileDefinition</c>/<c>RemovalPlan</c>. Esta independencia está
/// garantizada en tiempo de compilación: este proyecto de test no referencia
/// MRS.ComponentCatalog, MRS.ProfileEngine ni MRS.RemovalPlanning — si algún día
/// alguien intentase mezclar los dos conceptos desde aquí, el build fallaría
/// antes de llegar a ejecutar ningún test (mismo patrón de "guardia de
/// compilación" que la sonda de dependencias de P6).
/// </summary>
public sealed class InstallationOptionsTests
{
    [Fact]
    public void Default_values_are_all_enabled()
    {
        var options = InstallationOptionsModel.Default;

        Assert.True(options.AllowLocalAccount);
        Assert.True(options.AllowOfflineOobe);
        Assert.True(options.BypassTpm);
        Assert.True(options.BypassSecureBoot);
        Assert.True(options.BypassCpu);
        Assert.True(options.BypassRam);
        Assert.True(options.BypassStorage);
    }

    [Fact]
    public void Parameterless_constructor_matches_Default()
    {
        Assert.Equal(InstallationOptionsModel.Default, new InstallationOptionsModel());
    }

    [Theory]
    [InlineData(nameof(InstallationOptionsModel.AllowLocalAccount))]
    [InlineData(nameof(InstallationOptionsModel.AllowOfflineOobe))]
    [InlineData(nameof(InstallationOptionsModel.BypassTpm))]
    [InlineData(nameof(InstallationOptionsModel.BypassSecureBoot))]
    [InlineData(nameof(InstallationOptionsModel.BypassCpu))]
    [InlineData(nameof(InstallationOptionsModel.BypassRam))]
    [InlineData(nameof(InstallationOptionsModel.BypassStorage))]
    public void Each_bypass_is_independent_disabling_one_never_changes_the_others(string propertyName)
    {
        var baseline = InstallationOptionsModel.Default;
        var withOneDisabled = Disable(baseline, propertyName);

        foreach (var other in AllPropertyNames)
        {
            var expected = other == propertyName ? false : true;
            Assert.Equal(expected, Get(withOneDisabled, other));
        }
    }

    [Fact]
    public void Combining_several_disabled_options_does_not_affect_the_rest()
    {
        var options = InstallationOptionsModel.Default with { BypassTpm = false, AllowOfflineOobe = false };

        Assert.False(options.BypassTpm);
        Assert.False(options.AllowOfflineOobe);
        Assert.True(options.AllowLocalAccount);
        Assert.True(options.BypassSecureBoot);
        Assert.True(options.BypassCpu);
        Assert.True(options.BypassRam);
        Assert.True(options.BypassStorage);
    }

    [Fact]
    public void Serialization_round_trip_preserves_every_field()
    {
        var original = InstallationOptionsModel.Default with { BypassRam = false, BypassStorage = false, AllowLocalAccount = false };

        var json = InstallationOptionsSerializer.ToJson(original);
        var restored = InstallationOptionsSerializer.FromJson(json);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Empty_JSON_object_resolves_to_all_safe_defaults()
    {
        var restored = InstallationOptionsSerializer.FromJson("{}");

        Assert.Equal(InstallationOptionsModel.Default, restored);
    }

    [Fact]
    public void Null_or_blank_JSON_resolves_to_Default_without_throwing()
    {
        Assert.Equal(InstallationOptionsModel.Default, InstallationOptionsSerializer.FromJson(null));
        Assert.Equal(InstallationOptionsModel.Default, InstallationOptionsSerializer.FromJson("   "));
    }

    [Fact]
    public void Partial_JSON_only_overrides_the_fields_present_the_rest_stay_safe()
    {
        var restored = InstallationOptionsSerializer.FromJson("""{"bypassTpm": false}""");

        Assert.False(restored.BypassTpm);
        Assert.True(restored.AllowLocalAccount);
        Assert.True(restored.AllowOfflineOobe);
        Assert.True(restored.BypassSecureBoot);
        Assert.True(restored.BypassCpu);
        Assert.True(restored.BypassRam);
        Assert.True(restored.BypassStorage);
    }

    [Fact]
    public void Malformed_JSON_never_throws_via_FromJsonOrDefault()
    {
        var restored = InstallationOptionsSerializer.FromJsonOrDefault("{ esto no es JSON válido");

        Assert.Equal(InstallationOptionsModel.Default, restored);
    }

    [Fact]
    public void Serialization_is_deterministic_for_the_same_input()
    {
        var options = InstallationOptionsModel.Default with { BypassCpu = false };

        var first = InstallationOptionsSerializer.ToJson(options);
        var second = InstallationOptionsSerializer.ToJson(options);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Two_default_instances_are_equal_records_not_just_equivalent_by_reflection()
    {
        Assert.Equal(new InstallationOptionsModel(), new InstallationOptionsModel());
    }

    private static readonly string[] AllPropertyNames =
    {
        nameof(InstallationOptionsModel.AllowLocalAccount),
        nameof(InstallationOptionsModel.AllowOfflineOobe),
        nameof(InstallationOptionsModel.BypassTpm),
        nameof(InstallationOptionsModel.BypassSecureBoot),
        nameof(InstallationOptionsModel.BypassCpu),
        nameof(InstallationOptionsModel.BypassRam),
        nameof(InstallationOptionsModel.BypassStorage),
    };

    private static InstallationOptionsModel Disable(
        InstallationOptionsModel options, string propertyName) => propertyName switch
    {
        nameof(InstallationOptionsModel.AllowLocalAccount) => options with { AllowLocalAccount = false },
        nameof(InstallationOptionsModel.AllowOfflineOobe) => options with { AllowOfflineOobe = false },
        nameof(InstallationOptionsModel.BypassTpm) => options with { BypassTpm = false },
        nameof(InstallationOptionsModel.BypassSecureBoot) => options with { BypassSecureBoot = false },
        nameof(InstallationOptionsModel.BypassCpu) => options with { BypassCpu = false },
        nameof(InstallationOptionsModel.BypassRam) => options with { BypassRam = false },
        nameof(InstallationOptionsModel.BypassStorage) => options with { BypassStorage = false },
        _ => throw new ArgumentOutOfRangeException(nameof(propertyName)),
    };

    private static bool Get(InstallationOptionsModel options, string propertyName) => propertyName switch
    {
        nameof(InstallationOptionsModel.AllowLocalAccount) => options.AllowLocalAccount,
        nameof(InstallationOptionsModel.AllowOfflineOobe) => options.AllowOfflineOobe,
        nameof(InstallationOptionsModel.BypassTpm) => options.BypassTpm,
        nameof(InstallationOptionsModel.BypassSecureBoot) => options.BypassSecureBoot,
        nameof(InstallationOptionsModel.BypassCpu) => options.BypassCpu,
        nameof(InstallationOptionsModel.BypassRam) => options.BypassRam,
        nameof(InstallationOptionsModel.BypassStorage) => options.BypassStorage,
        _ => throw new ArgumentOutOfRangeException(nameof(propertyName)),
    };
}
