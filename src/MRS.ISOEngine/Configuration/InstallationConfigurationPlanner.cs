using MRS.ISOEngine.Models;
// "InstallationOptions" es a la vez un espacio de nombres (MRS.InstallationOptions) y el
// nombre del tipo que contiene (MRS.InstallationOptions.Models.InstallationOptions); dentro
// del árbol de namespaces "MRS.*" el namespace siempre gana en la búsqueda de nombres sin
// cualificar (mismo caso que MRS.RemovalEngine / MRS.ProfileEngine), así que se referencia
// el tipo con un alias explícito.
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Configuration;

/// <summary>
/// Traduce un <see cref="InstallationOptionsModel"/> ya elegido en la lista ordenada de
/// modificaciones que habría que aplicar al workspace de generación (P15). Función pura:
/// no toca ningún archivo, no ejecuta DISM, no depende de un <see cref="GenerationWorkspace"/>
/// concreto. El orden es determinista: primero Instalación (cuenta local, OOBE sin conexión),
/// luego Compatibilidad (TPM, Secure Boot, CPU, RAM, almacenamiento) — el mismo orden que
/// <c>InstallationOptions</c> y que los ejemplos de log del prompt. Una opción deshabilitada
/// simplemente no genera ninguna acción (nunca "genera una acción vacía o inválida").
/// </summary>
public static class InstallationConfigurationPlanner
{
    public static IReadOnlyList<InstallationConfigurationAction> Plan(InstallationOptionsModel options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var actions = new List<InstallationConfigurationAction>();

        if (options.AllowLocalAccount)
            actions.Add(new InstallationConfigurationAction
            {
                Category = InstallationActionCategory.Install,
                Description = "Local account enabled",
                Mechanism = "Answer file (autounattend.xml): bloque OOBE / UserAccounts de " +
                             "Microsoft-Windows-Shell-Setup. Mecanismo soportado oficialmente " +
                             "por Windows Setup para instalación desatendida.",
                TargetArtifact = "autounattend.xml (raíz de la ISO)",
            });

        if (options.AllowOfflineOobe)
            actions.Add(new InstallationConfigurationAction
            {
                Category = InstallationActionCategory.Install,
                Description = "Offline OOBE enabled",
                Mechanism = "Valor DWORD BypassNRO=1 en HKLM\\SYSTEM\\Setup\\OOBE del registro " +
                             "offline de boot.wim (el mismo mecanismo que %WinDir%\\System32\\Oobe\\" +
                             "BypassNRO.cmd aplica en caliente). El mecanismo está implementado " +
                             "(LabConfigApplier.ApplyOfflineOobeBypassAsync) pero P16 no confirmó su " +
                             "fiabilidad en 26H2 build 26300.9278 dentro de esta sesión (sin elevación " +
                             "disponible para probarlo) — no se aplica todavía automáticamente. Ver " +
                             "prompts/16-resultado.md.",
                TargetArtifact = "boot.wim (hive offline SYSTEM: Setup\\OOBE\\BypassNRO)",
                Status = InstallationActionStatus.PendingReliableMechanism,
            });

        if (options.BypassTpm)
            actions.Add(CompatAction("TPM bypass enabled",
                "BypassTPMCheck", "TPM 2.0"));

        if (options.BypassSecureBoot)
            actions.Add(CompatAction("Secure Boot bypass enabled",
                "BypassSecureBootCheck", "Secure Boot"));

        if (options.BypassCpu)
            actions.Add(CompatAction("CPU bypass enabled",
                "BypassCPUCheck", "CPU compatible"));

        if (options.BypassRam)
            actions.Add(CompatAction("RAM bypass enabled",
                "BypassRAMCheck", "RAM mínima") with
            {
                Mechanism = CompatMechanism("BypassRAMCheck", "RAM mínima") +
                             " Solo omite la comprobación del instalador; no garantiza que " +
                             "Windows 11 funcione con fluidez con RAM insuficiente (p. ej. 2 GB).",
            });

        if (options.BypassStorage)
            actions.Add(new InstallationConfigurationAction
            {
                Category = InstallationActionCategory.Compat,
                Description = "Storage bypass enabled",
                Mechanism = "Sin una clave LabConfig oficial equivalente confirmada para el " +
                             "requisito de almacenamiento (a diferencia de TPM/Secure Boot/CPU/RAM). " +
                             "No implementado: P16 bloquea la ejecución si esta opción está activada " +
                             "(ver InstallationExecutionValidator y prompts/16-resultado.md).",
                TargetArtifact = "boot.wim (hive offline SYSTEM: Setup\\LabConfig, sin confirmar)",
                Status = InstallationActionStatus.NotImplemented,
            });

        return actions;
    }

    private static InstallationConfigurationAction CompatAction(string description, string labConfigKey, string requirementLabel)
        => new()
        {
            Category = InstallationActionCategory.Compat,
            Description = description,
            Mechanism = CompatMechanism(labConfigKey, requirementLabel),
            TargetArtifact = $"boot.wim (hive offline SYSTEM: Setup\\LabConfig\\{labConfigKey})",
        };

    private static string CompatMechanism(string labConfigKey, string requirementLabel)
        => $"Valor DWORD {labConfigKey}=1 en HKLM\\SYSTEM\\Setup\\LabConfig del registro offline " +
           $"de boot.wim, comprobado por Setup antes de la pantalla de compatibilidad. Omite el " +
           $"requisito de {requirementLabel} del instalador.";
}
