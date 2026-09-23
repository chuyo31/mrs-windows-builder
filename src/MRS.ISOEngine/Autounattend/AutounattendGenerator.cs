using System.Xml.Linq;
using MRS.ISOEngine.Exceptions;
using MRS.ISOEngine.Models;

namespace MRS.ISOEngine.Autounattend;

/// <summary>
/// Genera <c>autounattend.xml</c> (P16, sección 5): el mecanismo soportado
/// oficialmente por Windows Setup para completar OOBE con una cuenta local, sin
/// depender de ninguna pantalla concreta (a diferencia de un hack de UI-automation).
/// Determinista: la misma <see cref="AutounattendConfiguration"/> siempre produce
/// exactamente el mismo XML. Separado a propósito de cualquier servicio de
/// ISO/Workspace/DISM: esta clase no sabe nada de rutas ni de montajes.
///
/// Ubicación (sección 6): Windows Setup busca <c>autounattend.xml</c> en la raíz
/// de cada unidad extraíble/óptica disponible (USB, unidad virtual de la ISO
/// montada) en el momento de arrancar. Colocarlo en la raíz de la ISO es
/// suficiente para que Setup lo detecte automáticamente — no es necesario
/// copiarlo dentro de <c>install.wim</c> ni de <c>boot.wim</c>. Si el usuario ya
/// aporta su propio archivo de respuesta (otro <c>autounattend.xml</c>, o uno
/// pasado por línea de comandos a Setup), ese archivo tiene prioridad: Setup
/// solo consulta el que encuentra primero según su orden de búsqueda estándar,
/// así que MRS nunca debería sobrescribir uno que el usuario ya haya puesto.
/// Este autounattend controla el paso <c>oobeSystem</c> (cuenta local, nombre
/// de equipo, y las pantallas de OOBE que se ocultan) y, si
/// <c>allowOfflineOobe</c> está activo, también el paso <c>specialize</c>
/// (P28): las opciones de <c>oobeSystem</c> por sí solas
/// (<c>HideOnlineAccountScreens</c>/<c>HideWirelessSetupInOOBE</c>) ocultan las
/// pantallas de cuenta en línea, pero no evitan la pantalla previa "Vamos a
/// conectarte a una red" — esa pantalla depende de
/// <c>HKLM\SYSTEM\Setup\OOBE\BypassNRO</c> en el sistema YA INSTALADO (el mismo
/// valor que deja <c>OOBE\BYPASSNRO.cmd</c> al ejecutarse manualmente), que
/// todavía no existe en ese momento porque install.wim aún no se ha aplicado a
/// disco. <c>specialize</c> es el paso de Setup que se ejecuta después de
/// aplicar la imagen y antes de que arranque OOBE, así que es el punto
/// correcto (y documentado por el propio esquema de unattend, componente
/// <c>Microsoft-Windows-Deployment</c>/<c>RunSynchronous</c>) para dejarlo ya
/// puesto de forma automática — sin ninguna intervención manual del usuario
/// durante OOBE. La prueba real de P28/P29 demostró que ese
/// <c>specialize</c> no basta por sí solo (la pantalla de red seguía
/// apareciendo): el mecanismo realmente fiable es aplicar el mismo valor
/// offline sobre el propio SYSTEM hive de <c>install.wim</c> (ver
/// <c>MRS.ISOEngine.InstallWim.InstallWimOobeConfigurator</c>, P29) antes de
/// que Setup lo copie a disco; <c>specialize</c> se conserva igualmente como
/// capa adicional, nunca perjudica.
///
/// Siempre incluye también un paso <c>windowsPE</c> (P29, sección 2) con
/// <c>Microsoft-Windows-Setup/UserData/AcceptEula</c>: es el paso que Setup
/// procesa desde el principio de la instalación, y su presencia es lo que
/// documenta el propio esquema de unattend para que Setup trate el resto del
/// archivo (specialize/oobeSystem) como autoritativo desde el arranque. No se
/// añade ningún componente de idioma/locale en <c>windowsPE</c>: no hay
/// ninguna configuración de idioma en <see cref="AutounattendConfiguration"/>
/// ni en <c>InstallationOptions</c>, y fijar uno a mano aquí sería un valor
/// inventado que podría no coincidir con el idioma real de la ISO.
/// </summary>
public static class AutounattendGenerator
{
    private static readonly XNamespace Ns = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace WcmNs = "http://schemas.microsoft.com/WMIConfig/2002/State";

    private const string InvalidAccountNameChars = "\"/\\[]:;|=,+*?<>";

    public static AutounattendValidationResult Validate(AutounattendConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.AccountName))
            errors.Add("El nombre de cuenta local no puede estar vacío.");
        else if (config.AccountName.Length > 20)
            errors.Add("El nombre de cuenta local no puede superar 20 caracteres (límite de Windows Setup).");
        else if (config.AccountName.Any(c => InvalidAccountNameChars.Contains(c)))
            errors.Add($"El nombre de cuenta local contiene caracteres no permitidos ({InvalidAccountNameChars}).");

        if (string.IsNullOrWhiteSpace(config.ComputerName))
            errors.Add("El nombre de equipo no puede estar vacío.");
        else if (config.ComputerName.Length > 15)
            errors.Add("El nombre de equipo no puede superar 15 caracteres (límite NetBIOS).");

        return errors.Count == 0 ? AutounattendValidationResult.Valid : new AutounattendValidationResult(false, errors);
    }

    /// <summary>
    /// Genera el XML. Lanza <see cref="IsoEngineException"/> si la configuración no
    /// es válida (llamar a <see cref="Validate"/> antes si se quiere decidir sin
    /// excepciones).
    /// </summary>
    public static string Generate(AutounattendConfiguration config, bool allowOfflineOobe)
    {
        var validation = Validate(config);
        if (!validation.IsValid)
            throw new IsoEngineException("Configuración de autounattend.xml inválida: " + string.Join("; ", validation.Errors));

        var localAccount = new XElement(Ns + "LocalAccount",
            new XAttribute(WcmNs + "action", "add"),
            new XElement(Ns + "Name", config.AccountName),
            new XElement(Ns + "Group", "Administrators"),
            new XElement(Ns + "DisplayName", config.AccountName));

        // Contraseña opcional: si no se proporciona, la cuenta se crea sin contraseña
        // (sección 5). Nunca hay un valor por defecto no vacío en el código.
        if (!string.IsNullOrEmpty(config.Password))
        {
            localAccount.AddFirst(new XElement(Ns + "Password",
                new XElement(Ns + "Value", config.Password),
                new XElement(Ns + "PlainText", "true")));
        }

        var oobe = new XElement(Ns + "OOBE",
            new XElement(Ns + "HideEULAPage", "true"),
            new XElement(Ns + "HideOEMRegistrationScreen", "true"),
            new XElement(Ns + "HideOnlineAccountScreens", "true"),
            new XElement(Ns + "HideWirelessSetupInOOBE", allowOfflineOobe ? "true" : "false"),
            new XElement(Ns + "ProtectYourPC", "3"));

        if (allowOfflineOobe)
            oobe.Add(new XElement(Ns + "NetworkLocation", "Home"));

        var shellSetup = new XElement(Ns + "component",
            new XAttribute("name", "Microsoft-Windows-Shell-Setup"),
            new XAttribute("processorArchitecture", "amd64"),
            new XAttribute("publicKeyToken", "31bf3856ad364e35"),
            new XAttribute("language", "neutral"),
            new XAttribute("versionScope", "nonSxS"),
            new XElement(Ns + "ComputerName", config.ComputerName),
            oobe,
            new XElement(Ns + "UserAccounts",
                new XElement(Ns + "LocalAccounts", localAccount)));

        var settingsOobeSystem = new XElement(Ns + "settings",
            new XAttribute("pass", "oobeSystem"),
            shellSetup);

        var root = new XElement(Ns + "unattend",
            new XAttribute(XNamespace.Xmlns + "wcm", WcmNs));

        // P29, sección 2: fase windowsPE válida, siempre presente (no depende de
        // ninguna opción). AcceptEula es el único campo real y documentado que
        // tiene sentido aquí sin inventar nada: no hay ProductKey que ofrecer, y
        // ninguna configuración de idioma existe todavía en InstallationOptions.
        var setupComponent = new XElement(Ns + "component",
            new XAttribute("name", "Microsoft-Windows-Setup"),
            new XAttribute("processorArchitecture", "amd64"),
            new XAttribute("publicKeyToken", "31bf3856ad364e35"),
            new XAttribute("language", "neutral"),
            new XAttribute("versionScope", "nonSxS"),
            new XElement(Ns + "UserData",
                new XElement(Ns + "AcceptEula", "true")));

        var settingsWindowsPe = new XElement(Ns + "settings",
            new XAttribute("pass", "windowsPE"),
            setupComponent);

        root.Add(settingsWindowsPe);

        // P28: BypassNRO debe quedar puesto en el sistema instalado ANTES de que
        // arranque OOBE. specialize es el primer paso de Setup que se ejecuta ya
        // sobre ese sistema (tras aplicar install.wim, antes de OOBE) — el punto
        // documentado del esquema de unattend para esto, no un hack de UI.
        if (allowOfflineOobe)
        {
            var runBypassNro = new XElement(Ns + "RunSynchronousCommand",
                new XAttribute(WcmNs + "action", "add"),
                new XElement(Ns + "Order", "1"),
                new XElement(Ns + "Path", @"reg add HKLM\SYSTEM\Setup\OOBE /v BypassNRO /t REG_DWORD /d 1 /f"),
                new XElement(Ns + "Description", "Bypass network requirement for OOBE"));

            var deploymentComponent = new XElement(Ns + "component",
                new XAttribute("name", "Microsoft-Windows-Deployment"),
                new XAttribute("processorArchitecture", "amd64"),
                new XAttribute("publicKeyToken", "31bf3856ad364e35"),
                new XAttribute("language", "neutral"),
                new XAttribute("versionScope", "nonSxS"),
                new XElement(Ns + "RunSynchronous", runBypassNro));

            var settingsSpecialize = new XElement(Ns + "settings",
                new XAttribute("pass", "specialize"),
                deploymentComponent);

            root.Add(settingsSpecialize);
        }

        root.Add(settingsOobeSystem);

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        return document.Declaration + Environment.NewLine + document.ToString(SaveOptions.None);
    }
}
