using MRS.ProfileEngine;
using MRS.ProfileEngine.Models;
using Xunit;

namespace MRS.ProfileEngine.Tests;

/// <summary>
/// P11: ProfileEngine solo carga JSON y produce una selección de ComponentId;
/// nunca ejecuta nada ni conoce DISM/WPF. Estos tests lo comprueban sin
/// depender de ningún catálogo real ni de RemovalEngine.
/// </summary>
public sealed class ProfileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-profile-tests", Guid.NewGuid().ToString("N"));
    private readonly IProfileService _service = new ProfileService();

    public ProfileServiceTests() => Directory.CreateDirectory(_dir);

    private void WriteProfile(string fileName, string json) =>
        File.WriteAllText(Path.Combine(_dir, fileName), json);

    [Fact]
    public void Loads_a_well_formed_profile_correctly()
    {
        WriteProfile("recommended.json", """
            {
              "id": "recommended",
              "name": "Recomendado",
              "description": "Perfil de prueba",
              "version": 1,
              "componentIds": ["appx:Clipchamp", "feature:Foo"]
            }
            """);

        var result = _service.LoadFromDirectory(_dir);

        Assert.False(result.HasErrors);
        Assert.Single(result.Profiles);
        var profile = result.Profiles[0];
        Assert.Equal("recommended", profile.Id);
        Assert.Equal("Recomendado", profile.Name);
        Assert.Equal(1, profile.Version);
        Assert.Equal(new[] { "appx:Clipchamp", "feature:Foo" }, profile.ComponentIds);
    }

    [Fact]
    public void GetProfile_returns_null_for_a_profile_id_that_does_not_exist()
    {
        WriteProfile("recommended.json", """{"id":"recommended","name":"Recomendado","version":1,"componentIds":[]}""");
        var result = _service.LoadFromDirectory(_dir);

        var profile = _service.GetProfile(result, "no-existe");

        Assert.Null(profile);
    }

    [Fact]
    public void Invalid_json_is_reported_as_an_error_without_blocking_the_rest()
    {
        WriteProfile("broken.json", "{ esto no es JSON válido");
        WriteProfile("recommended.json", """{"id":"recommended","name":"Recomendado","version":1,"componentIds":[]}""");

        var result = _service.LoadFromDirectory(_dir);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Code == ProfileValidationErrorCode.InvalidJson && e.FileName == "broken.json");
        Assert.Single(result.Profiles); // recommended.json se cargó igualmente
    }

    [Fact]
    public void Duplicate_profile_ids_are_reported_as_an_error()
    {
        WriteProfile("a.json", """{"id":"dup","name":"A","version":1,"componentIds":[]}""");
        WriteProfile("b.json", """{"id":"dup","name":"B","version":1,"componentIds":[]}""");

        var result = _service.LoadFromDirectory(_dir);

        Assert.Contains(result.Errors, e => e.Code == ProfileValidationErrorCode.DuplicateId);
        Assert.Single(result.Profiles); // solo el primero (a.json, orden alfabético) se conserva
        Assert.Equal("A", result.Profiles[0].Name);
    }

    [Fact]
    public void Missing_id_is_reported_as_an_error()
    {
        WriteProfile("noid.json", """{"name":"Sin id","version":1,"componentIds":[]}""");

        var result = _service.LoadFromDirectory(_dir);

        Assert.Contains(result.Errors, e => e.Code == ProfileValidationErrorCode.MissingId);
        Assert.Empty(result.Profiles);
    }

    [Fact]
    public void Missing_directory_is_reported_as_an_error_and_does_not_throw()
    {
        var result = _service.LoadFromDirectory(Path.Combine(_dir, "no-existe"));

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Code == ProfileValidationErrorCode.DirectoryNotFound);
        Assert.Empty(result.Profiles);
    }

    [Fact]
    public void GetSelection_marks_unknown_component_ids_and_never_selects_them()
    {
        var profile = new ProfileDefinition
        {
            Id = "p",
            ComponentIds = new[] { "appx:Real", "appx:NoExiste" },
        };

        var selection = _service.GetSelection(profile, knownComponentIds: new[] { "appx:Real" });

        Assert.Equal(new[] { "appx:Real" }, selection.SelectedComponentIds);
        Assert.Equal(new[] { "appx:NoExiste" }, selection.UnknownComponentIds);
    }

    [Fact]
    public void GetSelection_selects_everything_when_no_known_ids_are_supplied()
    {
        var profile = new ProfileDefinition { Id = "p", ComponentIds = new[] { "appx:A", "appx:B" } };

        var selection = _service.GetSelection(profile);

        Assert.Equal(new[] { "appx:A", "appx:B" }, selection.SelectedComponentIds);
        Assert.Empty(selection.UnknownComponentIds);
    }

    [Fact]
    public void GetSelection_flags_protected_ids_as_blocked_but_still_reports_them_as_selected()
    {
        var profile = new ProfileDefinition { Id = "p", ComponentIds = new[] { "appx:A", "feature:Protected" } };

        var selection = _service.GetSelection(
            profile,
            knownComponentIds: new[] { "appx:A", "feature:Protected" },
            protectedComponentIds: new[] { "feature:Protected" });

        // ProfileEngine informa del conflicto, pero no es quien decide: la protección
        // real la aplica ProtectionEngine/RemovalPlanning más adelante en el flujo.
        Assert.Contains("feature:Protected", selection.SelectedComponentIds);
        Assert.Equal(new[] { "feature:Protected" }, selection.BlockedComponentIds);
    }

    [Fact]
    public void Profiles_are_independent_of_each_other()
    {
        WriteProfile("minimal.json", """{"id":"minimal","name":"Mínimo","version":1,"componentIds":["appx:A"]}""");
        WriteProfile("clean.json", """{"id":"clean","name":"Limpio","version":1,"componentIds":["appx:B","appx:C"]}""");

        var result = _service.LoadFromDirectory(_dir);

        var minimal = _service.GetProfile(result, "minimal")!;
        var clean = _service.GetProfile(result, "clean")!;

        Assert.Equal(new[] { "appx:A" }, minimal.ComponentIds);
        Assert.Equal(new[] { "appx:B", "appx:C" }, clean.ComponentIds);
    }

    [Fact]
    public void Custom_profile_is_detected_by_convention_and_carries_no_fixed_selection()
    {
        WriteProfile("custom.json", """
            {"id":"custom","name":"Personalizado","version":1,"componentIds":[],"metadata":{"kind":"custom"}}
            """);

        var result = _service.LoadFromDirectory(_dir);
        var custom = _service.GetProfile(result, "custom")!;

        Assert.True(custom.IsCustom);
        Assert.Empty(custom.ComponentIds);
    }

    [Fact]
    public void ProfileEngine_never_executes_anything_it_only_produces_data()
    {
        // No hay ningún método en IProfileService que reciba un CancellationToken,
        // que devuelva un resultado de ejecución, ni que dependa de DISM/WPF: la
        // única superficie es "cargar JSON" y "calcular una selección de strings".
        // Este test documenta esa garantía comprobando la forma del contrato.
        var methods = typeof(IProfileService).GetMethods();

        Assert.All(methods, m => Assert.DoesNotContain("Execute", m.Name));
        Assert.All(methods, m => Assert.DoesNotContain("Run", m.Name));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }

    // ---- P13: SecurityOptions ------------------------------------------------

    [Fact]
    public void A_profile_with_no_securityOptions_in_JSON_defaults_to_safe_values()
    {
        WriteProfile("clean.json", """{"id":"clean","name":"Limpio","version":1,"componentIds":[]}""");

        var result = _service.LoadFromDirectory(_dir);
        var profile = _service.GetProfile(result, "clean")!;

        Assert.True(profile.SecurityOptions.KeepDefender);
        Assert.True(profile.SecurityOptions.KeepWindowsUpdate);
    }

    [Fact]
    public void A_profile_can_declare_securityOptions_in_JSON()
    {
        WriteProfile("clean.json", """
            {"id":"clean","name":"Limpio","version":1,"componentIds":[],
             "securityOptions":{"keepDefender":false,"keepWindowsUpdate":true}}
            """);

        var result = _service.LoadFromDirectory(_dir);
        var profile = _service.GetProfile(result, "clean")!;

        Assert.False(profile.SecurityOptions.KeepDefender);
        Assert.True(profile.SecurityOptions.KeepWindowsUpdate);
    }

    [Theory]
    [InlineData("minimal")]
    [InlineData("light")]
    [InlineData("recommended")]
    public void Minimal_light_and_recommended_always_keep_Defender_and_WindowsUpdate(string profileId)
    {
        // Aunque el JSON intentase desactivarlos, EffectiveSecurityOptions debe
        // seguir devolviendo siempre valores seguros para estos tres perfiles.
        WriteProfile($"{profileId}.json",
            "{\"id\":\"" + profileId + "\",\"name\":\"x\",\"version\":1,\"componentIds\":[]," +
            "\"securityOptions\":{\"keepDefender\":false,\"keepWindowsUpdate\":false}}");

        var result = _service.LoadFromDirectory(_dir);
        var profile = _service.GetProfile(result, profileId)!;

        Assert.True(profile.IsSecurityLocked);
        Assert.True(profile.EffectiveSecurityOptions.KeepDefender);
        Assert.True(profile.EffectiveSecurityOptions.KeepWindowsUpdate);
    }

    [Fact]
    public void Clean_allows_changing_Defender_via_its_own_SecurityOptions()
    {
        var profile = new ProfileDefinition
        {
            Id = "clean",
            SecurityOptions = new SecurityOptions { KeepDefender = false, KeepWindowsUpdate = true },
        };

        Assert.False(profile.IsSecurityLocked);
        Assert.False(profile.EffectiveSecurityOptions.KeepDefender);
        Assert.True(profile.EffectiveSecurityOptions.KeepWindowsUpdate);
    }

    [Fact]
    public void Clean_allows_changing_WindowsUpdate_via_its_own_SecurityOptions()
    {
        var profile = new ProfileDefinition
        {
            Id = "clean",
            SecurityOptions = new SecurityOptions { KeepDefender = true, KeepWindowsUpdate = false },
        };

        Assert.False(profile.IsSecurityLocked);
        Assert.True(profile.EffectiveSecurityOptions.KeepDefender);
        Assert.False(profile.EffectiveSecurityOptions.KeepWindowsUpdate);
    }

    [Fact]
    public void Custom_allows_changing_both_Defender_and_WindowsUpdate()
    {
        var profile = new ProfileDefinition
        {
            Id = "custom",
            Metadata = new Dictionary<string, string> { ["kind"] = "custom" },
            SecurityOptions = new SecurityOptions { KeepDefender = false, KeepWindowsUpdate = false },
        };

        Assert.True(profile.IsCustom);
        Assert.False(profile.IsSecurityLocked);
        Assert.False(profile.EffectiveSecurityOptions.KeepDefender);
        Assert.False(profile.EffectiveSecurityOptions.KeepWindowsUpdate);
    }

    [Fact]
    public void SecurityOptions_default_constructor_is_always_safe()
    {
        var defaults = new SecurityOptions();

        Assert.True(defaults.KeepDefender);
        Assert.True(defaults.KeepWindowsUpdate);
        Assert.Equal(SecurityOptions.Safe, defaults);
    }
}
