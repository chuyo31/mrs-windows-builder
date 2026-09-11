using MRS.ComponentCatalog.Models;

namespace MRS.ComponentCatalog.Rules;

/// <summary>
/// Base de reglas embebida, equivalente a
/// <c>catalog/win11/components.json</c> y <c>catalog/win11/protection-rules.json</c>.
/// Es pequeña y bien estructurada a propósito (Parte 9/10 del prompt): el
/// catálogo real lo produce <c>CatalogClassifier</c> a partir de lo que exista
/// en el inventario, estas reglas solo lo clasifican.
/// </summary>
public static class DefaultCatalogRules
{
    // Nota: Windows11 se declara al final del fichero para que su inicializador
    // se ejecute después de ClassificationRules/ProtectionRules (el orden de
    // inicialización de campos estáticos sigue el orden de declaración).

    // ---- Parte 4: identificación ------------------------------------------

    public static readonly IReadOnlyList<ClassificationRule> ClassificationRules = new[]
    {
        // Gaming
        new ClassificationRule { Id = "gaming-xbox", Pattern = "Xbox", Category = ComponentCategory.Gaming, Tags = new[] { "gaming" } },
        new ClassificationRule { Id = "gaming-app", Pattern = "GamingApp", Category = ComponentCategory.Gaming, Tags = new[] { "gaming" } },
        new ClassificationRule { Id = "gaming-solitaire", Pattern = "Solitaire", Category = ComponentCategory.Gaming, Tags = new[] { "gaming" } },

        // AI
        new ClassificationRule { Id = "ai-copilot", Pattern = "Copilot", Category = ComponentCategory.AI, Tags = new[] { "ai" } },

        // Comunicación
        new ClassificationRule { Id = "comm-outlook-new", Pattern = "OutlookForWindows", Category = ComponentCategory.Communication, Tags = new[] { "communication" } },
        new ClassificationRule { Id = "comm-outlook", Pattern = "Outlook", Category = ComponentCategory.Communication, Tags = new[] { "communication" } },
        new ClassificationRule { Id = "comm-teams", Pattern = "Teams", Category = ComponentCategory.Communication, Tags = new[] { "communication" } },
        new ClassificationRule { Id = "comm-skype", Pattern = "Skype", Category = ComponentCategory.Communication, Tags = new[] { "communication" } },
        new ClassificationRule { Id = "comm-phonelink", Pattern = "PhoneLink", Category = ComponentCategory.Communication, Tags = new[] { "communication" } },
        new ClassificationRule { Id = "comm-yourphone", Pattern = "YourPhone", Category = ComponentCategory.Communication, Tags = new[] { "communication" } },

        // Aplicaciones (Bing / Clipchamp / otras utilidades)
        new ClassificationRule { Id = "app-clipchamp", Pattern = "Clipchamp", Category = ComponentCategory.Application, Tags = new[] { "application" } },
        new ClassificationRule { Id = "app-bing-news", Pattern = "BingNews", Category = ComponentCategory.Application, Tags = new[] { "application", "bing" } },
        new ClassificationRule { Id = "app-bing-weather", Pattern = "BingWeather", Category = ComponentCategory.Application, Tags = new[] { "application", "bing" } },
        new ClassificationRule { Id = "app-bing-search", Pattern = "BingSearch", Category = ComponentCategory.Application, Tags = new[] { "application", "bing" } },

        // Store
        new ClassificationRule { Id = "store-purchase-app", Pattern = "StorePurchaseApp", Category = ComponentCategory.Store, Tags = new[] { "store" } },
        new ClassificationRule { Id = "store-engagement", Pattern = "StoreEngagement", Category = ComponentCategory.Store, Tags = new[] { "store" } },
        new ClassificationRule { Id = "store-windowsstore", Pattern = "WindowsStore", Category = ComponentCategory.Store, Tags = new[] { "store" } },

        // Frameworks
        new ClassificationRule { Id = "framework-vclibs", Pattern = "VCLibs", Category = ComponentCategory.Framework, Tags = new[] { "framework" } },
        new ClassificationRule { Id = "framework-net-native", Pattern = "NET.Native", Category = ComponentCategory.Framework, Tags = new[] { "framework" } },
        new ClassificationRule { Id = "framework-ui-xaml", Pattern = "UI.Xaml", Category = ComponentCategory.Framework, Tags = new[] { "framework" } },
        new ClassificationRule { Id = "framework-app-runtime", Pattern = "WindowsAppRuntime", Category = ComponentCategory.Framework, Tags = new[] { "framework" } },
        new ClassificationRule { Id = "framework-netfx", Pattern = "NetFx", Category = ComponentCategory.Framework, Tags = new[] { "framework" } },

        // Seguridad
        new ClassificationRule { Id = "security-defender", Pattern = "Defender", Category = ComponentCategory.Security, Tags = new[] { "security" } },
        new ClassificationRule { Id = "security-health", Pattern = "SecurityHealth", Category = ComponentCategory.Security, Tags = new[] { "security" } },

        // Windows Update / servicing
        new ClassificationRule { Id = "update-servicingstack", Pattern = "ServicingStack", Category = ComponentCategory.WindowsUpdate, Tags = new[] { "servicing" } },
        new ClassificationRule { Id = "update-cbs", Pattern = "CBS", Category = ComponentCategory.WindowsUpdate, Tags = new[] { "servicing" } },
        new ClassificationRule { Id = "update-ssu", Pattern = "SSU", Category = ComponentCategory.WindowsUpdate, Tags = new[] { "servicing" } },
        new ClassificationRule { Id = "update-lcu", Pattern = "LCU", Category = ComponentCategory.WindowsUpdate, Tags = new[] { "servicing" } },
        new ClassificationRule { Id = "update-windowsupdate", Pattern = "WindowsUpdate", Category = ComponentCategory.WindowsUpdate, Tags = new[] { "servicing" } },

        // Redes
        new ClassificationRule { Id = "net-wifi", Pattern = "Wi-Fi", Category = ComponentCategory.Networking, Tags = new[] { "networking" } },
        new ClassificationRule { Id = "net-wifi2", Pattern = "WiFi", Category = ComponentCategory.Networking, Tags = new[] { "networking" } },
        new ClassificationRule { Id = "net-bluetooth", Pattern = "Bluetooth", Category = ComponentCategory.Networking, Tags = new[] { "networking" } },
        new ClassificationRule { Id = "net-ethernet", Pattern = "Ethernet", Category = ComponentCategory.Networking, Tags = new[] { "networking" } },
        new ClassificationRule { Id = "net-networking", Pattern = "Networking", Category = ComponentCategory.Networking, Tags = new[] { "networking" } },

        // Impresión
        new ClassificationRule { Id = "print-printing", Pattern = "Printing", Category = ComponentCategory.Printing, Tags = new[] { "printing" } },
        new ClassificationRule { Id = "print-spooler", Pattern = "Spooler", Category = ComponentCategory.Printing, Tags = new[] { "printing" } },
        new ClassificationRule { Id = "print-xps", Pattern = "XPS", Category = ComponentCategory.Printing, Tags = new[] { "printing" } },
        new ClassificationRule { Id = "print-generic", Pattern = "Print", Category = ComponentCategory.Printing, Tags = new[] { "printing" } },

        // Medios
        new ClassificationRule { Id = "media-foundation", Pattern = "MediaFoundation", Category = ComponentCategory.Media, Tags = new[] { "media" } },
        new ClassificationRule { Id = "media-player", Pattern = "MediaPlayer", Category = ComponentCategory.Media, Tags = new[] { "media" } },
        new ClassificationRule { Id = "media-playback", Pattern = "MediaPlayback", Category = ComponentCategory.Media, Tags = new[] { "media" } },
        new ClassificationRule { Id = "media-codec", Pattern = "Codec", Category = ComponentCategory.Media, Tags = new[] { "media" } },

        // Idioma / accesibilidad / desarrollo
        new ClassificationRule { Id = "language-basic", Pattern = "Language.Basic", Category = ComponentCategory.Language, Tags = new[] { "language" } },
        new ClassificationRule { Id = "language-features", Pattern = "LanguageFeatures", Category = ComponentCategory.Language, Tags = new[] { "language" } },
        new ClassificationRule { Id = "accessibility", Pattern = "Accessibility", Category = ComponentCategory.Accessibility, Tags = new[] { "accessibility" } },
        new ClassificationRule { Id = "dev-tools", Pattern = "DeveloperTools", Category = ComponentCategory.Development, Tags = new[] { "development" } },
        new ClassificationRule { Id = "dev-openssh", Pattern = "OpenSSH", Category = ComponentCategory.Development, Tags = new[] { "development" } },
        new ClassificationRule { Id = "dev-wsl", Pattern = "WSL", Category = ComponentCategory.Development, Tags = new[] { "development" } },

        // Telemetría
        new ClassificationRule { Id = "telemetry", Pattern = "Telemetry", Category = ComponentCategory.Telemetry, Tags = new[] { "telemetry" } },

        // Fallback genérico de sistema (solo clasifica, no protege)
        new ClassificationRule { Id = "system-generic", Pattern = "Microsoft-Windows-", Category = ComponentCategory.System, Tags = new[] { "system" } },
    };

    // ---- Parte 5/8: protección --------------------------------------------

    public static readonly IReadOnlyList<ProtectionRule> ProtectionRules = new[]
    {
        new ProtectionRule
        {
            Id = "windows-servicing", Pattern = "ServicingStack", Risk = ComponentRisk.Critical,
            Category = ComponentCategory.WindowsUpdate,
            Reason = "Servicing Stack: necesario para instalar y reparar actualizaciones de Windows.",
        },
        new ProtectionRule
        {
            Id = "component-based-servicing", Pattern = "CBS", Risk = ComponentRisk.Critical,
            Category = ComponentCategory.WindowsUpdate,
            Reason = "Component Based Servicing: necesario para instalar/reparar componentes de Windows.",
        },
        new ProtectionRule
        {
            Id = "servicing-ssu", Pattern = "SSU", Risk = ComponentRisk.Critical,
            Category = ComponentCategory.WindowsUpdate,
            Reason = "Servicing Stack Update: requerido para aplicar actualizaciones con seguridad.",
        },
        new ProtectionRule
        {
            Id = "servicing-lcu", Pattern = "LCU", Risk = ComponentRisk.Critical,
            Category = ComponentCategory.WindowsUpdate,
            Reason = "Latest Cumulative Update: actualización acumulativa de Windows Update.",
        },
        new ProtectionRule
        {
            Id = "windows-update", Pattern = "WindowsUpdate", Risk = ComponentRisk.Critical,
            Category = ComponentCategory.WindowsUpdate,
            Reason = "Necesario para el funcionamiento de Windows Update.",
        },
        new ProtectionRule
        {
            Id = "windows-defender", Pattern = "Defender", Risk = ComponentRisk.Critical,
            Category = ComponentCategory.Security,
            Reason = "Motor de protección de Microsoft Defender / Seguridad de Windows.",
        },
        new ProtectionRule
        {
            Id = "security-health", Pattern = "SecurityHealth", Risk = ComponentRisk.Critical,
            Category = ComponentCategory.Security,
            Reason = "Componente de Seguridad de Windows (estado de seguridad del sistema).",
        },
        new ProtectionRule
        {
            Id = "microsoft-store-core", Pattern = "WindowsStore", Risk = ComponentRisk.High,
            Category = ComponentCategory.Store,
            Reason = "Aplicación base de Microsoft Store.",
        },
        new ProtectionRule
        {
            Id = "store-purchase-app", Pattern = "StorePurchaseApp", Risk = ComponentRisk.High,
            Category = ComponentCategory.Store,
            Reason = "Componente fundamental de Microsoft Store (gestión de compras).",
        },
        new ProtectionRule
        {
            Id = "windows-installer", Pattern = "Windows Installer", Risk = ComponentRisk.Critical,
            Category = ComponentCategory.System,
            Reason = "Windows Installer: necesario para instalar y reparar software.",
        },
        new ProtectionRule
        {
            Id = "winre", Pattern = "WinRE", Risk = ComponentRisk.Critical,
            Category = ComponentCategory.System,
            Reason = "Entorno de recuperación de Windows (WinRE).",
        },
        new ProtectionRule
        {
            Id = "networking-wifi", Pattern = "Wi-Fi", Risk = ComponentRisk.High,
            Category = ComponentCategory.Networking,
            Reason = "Necesario para la conectividad Wi-Fi básica del sistema.",
        },
        new ProtectionRule
        {
            Id = "networking-wifi2", Pattern = "WiFi", Risk = ComponentRisk.High,
            Category = ComponentCategory.Networking,
            Reason = "Necesario para la conectividad Wi-Fi básica del sistema.",
        },
        new ProtectionRule
        {
            Id = "networking-ethernet", Pattern = "Ethernet", Risk = ComponentRisk.High,
            Category = ComponentCategory.Networking,
            Reason = "Necesario para la conectividad de red por cable.",
        },
        new ProtectionRule
        {
            Id = "networking-bluetooth", Pattern = "Bluetooth", Risk = ComponentRisk.High,
            Category = ComponentCategory.Networking,
            Reason = "Necesario para la conectividad Bluetooth básica del sistema.",
        },
        new ProtectionRule
        {
            Id = "usb", Pattern = "USB", Risk = ComponentRisk.High,
            Category = ComponentCategory.System,
            Reason = "Necesario para el funcionamiento de dispositivos USB.",
        },
        new ProtectionRule
        {
            Id = "audio", Pattern = "Audio", Risk = ComponentRisk.High,
            Category = ComponentCategory.System,
            Reason = "Necesario para el subsistema de audio de Windows.",
        },
        new ProtectionRule
        {
            Id = "printing-core", Pattern = "Print", Risk = ComponentRisk.Medium,
            Category = ComponentCategory.Printing,
            Reason = "Necesario para la funcionalidad de impresión del sistema.",
        },
        new ProtectionRule
        {
            Id = "dotnet-framework", Pattern = "NetFx", Risk = ComponentRisk.High,
            Category = ComponentCategory.Framework,
            Reason = "Framework .NET necesario para aplicaciones de Windows.",
        },
        new ProtectionRule
        {
            Id = "windows-app-runtime", Pattern = "WindowsAppRuntime", Risk = ComponentRisk.High,
            Category = ComponentCategory.Framework,
            Reason = "Windows App Runtime: dependencia habitual de aplicaciones modernas.",
        },
        new ProtectionRule
        {
            Id = "vclibs", Pattern = "VCLibs", Risk = ComponentRisk.High,
            Category = ComponentCategory.Framework,
            Reason = "Framework utilizado por otras aplicaciones AppX.",
        },
        new ProtectionRule
        {
            Id = "ui-xaml", Pattern = "UI.Xaml", Risk = ComponentRisk.High,
            Category = ComponentCategory.Framework,
            Reason = "Framework utilizado por aplicaciones Windows modernas (UI.Xaml).",
        },
        new ProtectionRule
        {
            Id = "base-system-package", Pattern = "Foundation-Package", Risk = ComponentRisk.Critical,
            Category = ComponentCategory.System,
            Reason = "Paquete base del sistema operativo.",
        },
        new ProtectionRule
        {
            Id = "language-pack", Pattern = "LanguageFeatures-Basic", Risk = ComponentRisk.Medium,
            Category = ComponentCategory.Language,
            Reason = "Paquete de idioma necesario para la interfaz en el idioma instalado.",
        },
        new ProtectionRule
        {
            Id = "oobe", Pattern = "OOBE", Risk = ComponentRisk.High,
            Category = ComponentCategory.System,
            Reason = "Necesario para la configuración inicial de Windows (OOBE).",
        },
    };

    public static CatalogRuleSet Windows11 { get; } = new(ClassificationRules, ProtectionRules);
}
