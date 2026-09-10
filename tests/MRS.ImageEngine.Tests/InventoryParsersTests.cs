using MRS.ImageEngine.Parsing;
using MRS.ImageEngine.Tests.Data;
using Xunit;

namespace MRS.ImageEngine.Tests;

public class InventoryParsersTests
{
    [Fact]
    public void Packages_are_parsed_with_all_fields()
    {
        var packages = new DismPackageParser().Parse(DismOutputs.Packages);

        Assert.Equal(3, packages.Count);
        var first = packages[0];
        Assert.Equal("Microsoft-Windows-Foundation-Package~31bf3856ad364e35~amd64~~10.0.26300.9278", first.PackageIdentity);
        Assert.Equal("Installed", first.State);
        Assert.Equal("Foundation", first.ReleaseType);
        Assert.Equal("1/10/2025 3:00 PM", first.InstallTime);
        Assert.Null(first.Description);
    }

    [Fact]
    public void Features_are_parsed_with_state()
    {
        var features = new DismFeatureParser().Parse(DismOutputs.Features);

        Assert.Equal(3, features.Count);
        Assert.Equal("NetFx4-AdvSrvs", features[0].Name);
        Assert.Equal("Enabled", features[0].State);
        Assert.Equal("Disabled", features[1].State);
    }

    [Fact]
    public void Capabilities_are_parsed_with_state()
    {
        var capabilities = new DismCapabilityParser().Parse(DismOutputs.Capabilities);

        Assert.Equal(3, capabilities.Count);
        Assert.Equal("Language.Basic~~~es-ES~0.0.1.0", capabilities[0].Identity);
        Assert.Equal("Not Present", capabilities[1].State);
    }

    [Fact]
    public void Provisioned_apps_are_parsed_and_publisher_id_is_derived_when_absent()
    {
        var apps = new DismProvisionedAppParser().Parse(DismOutputs.ProvisionedApps);

        Assert.Equal(2, apps.Count);

        var calculator = apps[0];
        Assert.Equal("Microsoft.WindowsCalculator", calculator.DisplayName);
        Assert.Equal("2021.2402.2.0", calculator.Version);
        Assert.Equal("neutral", calculator.Architecture);
        Assert.Equal("~", calculator.ResourceId);
        Assert.Equal("8wekyb3d8bbwe", calculator.PublisherId); // derivado del PackageName

        Assert.Equal("8wekyb3d8bbwe", apps[1].PublisherId); // explícito en la salida
    }

    [Fact]
    public void Drivers_are_parsed_including_boot_critical()
    {
        var drivers = new DismDriverParser().Parse(DismOutputs.Drivers);

        Assert.Equal(2, drivers.Count);
        var printer = drivers[0];
        Assert.Equal("oem0.inf", printer.PublishedName);
        Assert.Equal("prnms003.inf", printer.OriginalFileName);
        Assert.Equal("Microsoft", printer.Provider);
        Assert.Equal("Printer", printer.Class);
        Assert.Equal("6/21/2006", printer.Date);
        Assert.False(printer.BootCritical);
        Assert.Equal("NVIDIA", drivers[1].Provider);
    }

    [Theory]
    [InlineData("")]
    [InlineData("The operation completed successfully.")]
    [InlineData("Deployment Image Servicing and Management tool\nVersion: 10.0.26100.1150\n")]
    public void Parsers_return_empty_for_empty_or_bannerless_output(string output)
    {
        Assert.Empty(new DismPackageParser().Parse(output));
        Assert.Empty(new DismFeatureParser().Parse(output));
        Assert.Empty(new DismCapabilityParser().Parse(output));
        Assert.Empty(new DismProvisionedAppParser().Parse(output));
        Assert.Empty(new DismDriverParser().Parse(output));
    }

    [Fact]
    public void Parser_is_tolerant_to_property_order_and_optional_fields()
    {
        const string reordered = """
            Image Version: 10.0.26300.0

            Package Identity : Some-Package~31bf3856ad364e35~amd64~~10.0.0.0
            Install Time : 2/2/2025 1:00 PM
            Release Type : Update
            State : Installed

            Package Identity : Other-Package~31bf3856ad364e35~amd64~~10.0.0.0
            State : Superseded
            """;

        var packages = new DismPackageParser().Parse(reordered);

        Assert.Equal(2, packages.Count);
        Assert.Equal("Some-Package~31bf3856ad364e35~amd64~~10.0.0.0", packages[0].PackageIdentity);
        Assert.Equal("Installed", packages[0].State);
        Assert.Equal("Update", packages[0].ReleaseType);
        Assert.Equal("Superseded", packages[1].State);
        Assert.Null(packages[1].ReleaseType); // campo opcional ausente
    }

    [Theory]
    [InlineData("Yes", true)]
    [InlineData("no", false)]
    [InlineData("", null)]
    [InlineData("maybe", null)]
    public void YesNo_is_tolerant(string raw, bool? expected)
    {
        Assert.Equal(expected, DismBlockReader.YesNo(raw));
    }
}
