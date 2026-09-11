using MRS.ImageEngine.Models;

namespace MRS.ComponentCatalog.Tests;

/// <summary>Construye un <see cref="ImageInventory"/> de ejemplo sin usar una ISO real.</summary>
internal static class FakeInventory
{
    public static ImageInventory Sample() => new()
    {
        Packages = new[]
        {
            new ImagePackage { PackageIdentity = "Microsoft-Windows-Foundation-Package~31bf3856ad364e35~amd64~~10.0.26300.9278", State = "Installed" },
            new ImagePackage { PackageIdentity = "Microsoft-Windows-ServicingStack-OnDemand-Package~31bf3856ad364e35~amd64~~10.0.26300.9278", State = "Installed" },
            new ImagePackage { PackageIdentity = "Package_for_SSU~31bf3856ad364e35~amd64~~26300.9278.1.1", State = "Installed" },
            new ImagePackage { PackageIdentity = "Package_for_KB5044384~31bf3856ad364e35~amd64~LCU~26300.9278.1.1", State = "Superseded" },
            new ImagePackage { PackageIdentity = "Microsoft-Windows-LanguageFeatures-Basic-es-es-Package~31bf3856ad364e35~amd64~~10.0.26300.9278", State = "Installed" },
            new ImagePackage { PackageIdentity = "Microsoft-Windows-NetFx4-US-Package~31bf3856ad364e35~amd64~~10.0.26300.9278", State = "Installed" },
            new ImagePackage { PackageIdentity = "Microsoft-Windows-Printing-PrintToPDF-Package~31bf3856ad364e35~amd64~~10.0.26300.9278", State = "Installed" },
            new ImagePackage { PackageIdentity = "Microsoft-Windows-Client-LanguagePack-Package~es-ES~31bf3856ad364e35~10.0.26300.9278", State = "Installed" },
            new ImagePackage { PackageIdentity = "Contoso.SampleThirdPartyWidget~00000000000000~amd64~~1.0.0.0", State = "Installed" },
            new ImagePackage { PackageIdentity = "Microsoft-Windows-Defender-Package~31bf3856ad364e35~amd64~~10.0.26300.9278", State = "Installed" },
            new ImagePackage { PackageIdentity = "Microsoft-Windows-WinRE-Package~31bf3856ad364e35~amd64~~10.0.26300.9278", State = "Installed" },
        },
        ProvisionedApps = new[]
        {
            new ProvisionedApp { DisplayName = "Microsoft.XboxApp", PackageName = "Microsoft.XboxApp_2024.1.1_neutral_~_8wekyb3d8bbwe" },
            new ProvisionedApp { DisplayName = "MicrosoftCorporationII.MicrosoftClipchamp", PackageName = "MicrosoftCorporationII.MicrosoftClipchamp_2.0.0.0_neutral_~_8wekyb3d8bbwe" },
            new ProvisionedApp { DisplayName = "Microsoft.Copilot", PackageName = "Microsoft.Copilot_1.0.0.0_neutral_~_8wekyb3d8bbwe" },
            new ProvisionedApp { DisplayName = "Microsoft.WindowsStore", PackageName = "Microsoft.WindowsStore_22411.1401.0.0_neutral_~_8wekyb3d8bbwe" },
            new ProvisionedApp { DisplayName = "Microsoft.StorePurchaseApp", PackageName = "Microsoft.StorePurchaseApp_22102.1401.0.0_neutral_~_8wekyb3d8bbwe" },
            new ProvisionedApp { DisplayName = "Microsoft.Paint", PackageName = "Microsoft.Paint_11.2405.0.0_neutral_~_8wekyb3d8bbwe" },
            new ProvisionedApp { DisplayName = "Microsoft.VCLibs.140.00", PackageName = "Microsoft.VCLibs.140.00_14.0.33321.0_neutral_~_8wekyb3d8bbwe" },
            new ProvisionedApp { DisplayName = "Microsoft.UI.Xaml.2.8", PackageName = "Microsoft.UI.Xaml.2.8_8.2405.0.0_neutral_~_8wekyb3d8bbwe" },
        },
        Features = new[]
        {
            new ImageFeature { Name = "NetFx4-AdvSrvs", State = "Enabled" },
            new ImageFeature { Name = "MediaPlayback", State = "Enabled" },
            new ImageFeature { Name = "Printing-Foundation-Features", State = "Disabled" },
            new ImageFeature { Name = "WiFiDirect-Services", State = "Enabled" },
        },
        Capabilities = new[]
        {
            new ImageCapability { Identity = "Language.Basic~~~es-ES~0.0.1.0", State = "Installed" },
            new ImageCapability { Identity = "OpenSSH.Client~~~~0.0.1.0", State = "Installed" },
            new ImageCapability { Identity = "Browser.InternetExplorer~~~~0.0.11.0", State = "Not Present" },
        },
        Drivers = new[]
        {
            new ImageDriver { PublishedName = "oem0.inf", OriginalFileName = "nvhda.inf", Provider = "NVIDIA", Class = "MEDIA" },
        },
    };
}
