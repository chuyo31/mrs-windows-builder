namespace MRS.ImageEngine.Tests.Data;

/// <summary>
/// Salidas de DISM /English reproducidas para los tests. Ninguna prueba
/// necesita una ISO real.
/// </summary>
internal static class DismOutputs
{
    /// <summary>DISM /Get-WimInfo (listado) con dos ediciones.</summary>
    public const string ListTwoEditions = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Details for image : X:\sources\install.wim

        Index : 1
        Name : Windows 11 Home
        Description : Windows 11 Home
        Size : 16.000.000.000 bytes

        Index : 2
        Name : Windows 11 Pro
        Description : Windows 11 Pro
        Size : 16.200.000.000 bytes

        The operation completed successfully.
        """;

    /// <summary>DISM /Get-WimInfo (listado) con un solo índice.</summary>
    public const string ListSingleEdition = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Details for image : X:\sources\install.esd

        Index : 1
        Name : Windows 11 Pro
        Description : Windows 11 Pro
        Size : 4.100.000.000 bytes

        The operation completed successfully.
        """;

    /// <summary>Detalle del índice 1 (Home), idioma en línea de continuación.</summary>
    public const string DetailIndex1 = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Details for image : X:\sources\install.wim

        Index : 1
        Name : Windows 11 Home
        Description : Windows 11 Home
        Size : 16,000,000,000 bytes
        WIM Bootable : No
        Architecture : x64
        Hal : <undefined>
        Version : 10.0.26300
        ServicePack Build : 9278
        ServicePack Level : 0
        Edition : Core
        Installation : Client
        ProductType : WinNT
        ProductSuite : Terminal Server
        System Root : WINDOWS
        Directories : 21735
        Files : 98243
        Created : 01/10/2025 - 15:00:00
        Modified : 01/10/2025 - 15:10:00
        Languages :
                es-ES (Default)

        The operation completed successfully.
        """;

    /// <summary>Detalle del índice 2 (Pro), idioma en la misma línea.</summary>
    public const string DetailIndex2 = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Details for image : X:\sources\install.wim

        Index : 2
        Name : Windows 11 Pro
        Description : Windows 11 Pro
        Size : 16,200,000,000 bytes
        WIM Bootable : No
        Architecture : x64
        Version : 10.0.26300
        ServicePack Build : 9278
        Edition : Professional
        Installation : Client
        Languages : es-ES (Default)

        The operation completed successfully.
        """;

    /// <summary>Detalle de una imagen ARM64 (Windows 10 21H2) para variar arquitectura/versión.</summary>
    public const string DetailArm64Windows10 = """
        Deployment Image Servicing and Management tool
        Version: 10.0.22621.1

        Details for image : X:\sources\install.wim

        Index : 1
        Name : Windows 10 Pro
        Description : Windows 10 Pro
        Architecture : ARM64
        Version : 10.0.19044
        ServicePack Build : 1288
        Edition : Professional
        Languages : en-US (Default)

        The operation completed successfully.
        """;

    public const string DismFailure = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Error: 0xc1420127

        The specified image in the specified wim is already mounted for read/write access.

        The DISM log file can be found at C:\Windows\Logs\DISM\dism.log
        """;

    // --- Inventario (fase 3) --------------------------------------------------

    public const string Packages = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Image Version: 10.0.26300.0

        Packages listing:

        Package Identity : Microsoft-Windows-Foundation-Package~31bf3856ad364e35~amd64~~10.0.26300.9278
        State : Installed
        Release Type : Foundation
        Install Time : 1/10/2025 3:00 PM

        Package Identity : Package_for_ServicingStack~31bf3856ad364e35~amd64~~10.0.26300.9278
        State : Installed
        Release Type : Update
        Install Time : 1/10/2025 3:05 PM

        Package Identity : Microsoft-Windows-LanguageFeatures-Basic-es-es-Package~31bf3856ad364e35~amd64~~10.0.26300.9278
        State : Installed
        Release Type : LanguagePack
        Install Time : 1/10/2025 3:06 PM

        The operation completed successfully.
        """;

    public const string Features = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Image Version: 10.0.26300.0

        Features listing for package : Microsoft-Windows-Foundation-Package~31bf3856ad364e35~amd64~~10.0.26300.9278

        Feature Name : NetFx4-AdvSrvs
        State : Enabled

        Feature Name : Printing-Foundation-Features
        State : Disabled

        Feature Name : MediaPlayback
        State : Enabled

        The operation completed successfully.
        """;

    public const string Capabilities = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Image Version: 10.0.26300.0

        Capability Identity : Language.Basic~~~es-ES~0.0.1.0
        State : Installed

        Capability Identity : Browser.InternetExplorer~~~~0.0.11.0
        State : Not Present

        Capability Identity : OpenSSH.Client~~~~0.0.1.0
        State : Installed

        The operation completed successfully.
        """;

    public const string ProvisionedApps = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Image Version: 10.0.26300.0

        DisplayName : Microsoft.WindowsCalculator
        Version : 2021.2402.2.0
        Architecture : neutral
        ResourceId : ~
        PackageName : Microsoft.WindowsCalculator_2021.2402.2.0_neutral_~_8wekyb3d8bbwe
        Regions :

        DisplayName : Microsoft.Windows.Photos
        Version : 2024.11020.19010.0
        Architecture : neutral
        ResourceId : ~
        PackageName : Microsoft.Windows.Photos_2024.11020.19010.0_neutral_~_8wekyb3d8bbwe
        PublisherId : 8wekyb3d8bbwe

        The operation completed successfully.
        """;

    public const string Drivers = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Image Version: 10.0.26300.0

        Obtaining list of 3rd party drivers from the driver store...

        Driver packages listing:

        Published Name : oem0.inf
        Original File Name : prnms003.inf
        Inbox : No
        Class Name : Printer
        Provider Name : Microsoft
        Date : 6/21/2006
        Version : 10.0.26300.9278
        Boot Critical : No

        Published Name : oem1.inf
        Original File Name : nvhda.inf
        Inbox : No
        Class Name : MEDIA
        Provider Name : NVIDIA
        Date : 3/14/2024
        Version : 1.4.1.0
        Boot Critical : No

        The operation completed successfully.
        """;

    public const string MountedImageInfoEmpty = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        No mounted images were found.

        The operation completed successfully.
        """;

    public static string MountedImageInfoWith(string mountDir) => $$"""
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Mount Dir : {{mountDir}}
        Image File : X:\sources\install.wim
        Image Index : 2
        Mounted Read/Write : No
        Status : Ok

        The operation completed successfully.
        """;
}
