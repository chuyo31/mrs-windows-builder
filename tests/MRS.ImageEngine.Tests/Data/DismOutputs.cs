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
}
