using System.Xml.Linq;
using MRS.DismEngine.Dism;
using MRS.DismEngine.Logging;
using MRS.ISOEngine.Autounattend;
using MRS.ISOEngine.BootWim;
using MRS.ISOEngine.Configuration;
using MRS.ISOEngine.Exceptions;
using MRS.ISOEngine.Models;
using MRS.ISOEngine.Registry;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine;

/// <summary>Implementación de <see cref="IInstallationImageService"/>. Ver la interfaz para el diagrama de responsabilidad.</summary>
public sealed class InstallationImageService : IInstallationImageService
{
    // P28: auditoría real sobre una ISO generada por MRS confirmó que boot.wim
    // tiene índice 1 = Microsoft Windows PE (x64) e índice 2 = Microsoft Windows
    // Setup (x64) -- no "índice 2 = Recuperación" como asumía P16. LabConfig
    // debe quedar aplicado en AMBOS: índice 2 es el entorno donde Windows Setup
    // ejecuta de verdad la comprobación de hardware (TPM/Secure Boot/CPU/RAM),
    // así que aplicarlo solo al índice 1 (WinPE) lo dejaba sin efecto real.
    private static readonly int[] BootWimIndicesRequiringLabConfig = { 1, 2 };

    // El bypass de red offline (BypassNRO) solo tiene sentido en el índice de
    // Windows Setup: es el entorno que arranca antes de que empiece OOBE.
    private const int WindowsSetupBootWimIndex = 2;

    private readonly IBootWimProvisioner _bootWimProvisioner;
    private readonly IBootWimModifier _bootWimModifier;
    // P28: solo para ValidateFinalAsync (re-montar de solo lectura tras el
    // commit). ApplyAsync sigue delegando todo el ciclo Mount(RW)/hive/Commit en
    // _bootWimModifier, sin usar estos dos directamente.
    private readonly IDismRunner? _dism;
    private readonly IOfflineRegistryEditor? _registry;
    private readonly LabConfigApplier? _labConfigApplier;
    private readonly IAppLogger _logger;

    public InstallationImageService(
        IBootWimProvisioner bootWimProvisioner, IBootWimModifier bootWimModifier, IAppLogger? logger = null)
        : this(bootWimProvisioner, bootWimModifier, dismRunner: null, registry: null, logger)
    {
    }

    /// <summary>
    /// P28: sobrecarga que además permite <see cref="ValidateFinalAsync"/>. Los
    /// dos parámetros nuevos son opcionales para no romper el constructor ya
    /// usado por P16-P24 (tests y wiring existentes); si se omiten,
    /// <see cref="ValidateFinalAsync"/> devuelve un resultado válido sin
    /// comprobar nada, en vez de lanzar -- documentado explícitamente en el
    /// propio método, nunca silencioso.
    /// </summary>
    public InstallationImageService(
        IBootWimProvisioner bootWimProvisioner, IBootWimModifier bootWimModifier,
        IDismRunner? dismRunner, IOfflineRegistryEditor? registry, IAppLogger? logger = null)
    {
        _bootWimProvisioner = bootWimProvisioner ?? throw new ArgumentNullException(nameof(bootWimProvisioner));
        _bootWimModifier = bootWimModifier ?? throw new ArgumentNullException(nameof(bootWimModifier));
        _dism = dismRunner;
        _registry = registry;
        _logger = logger ?? NullAppLogger.Instance;
        _labConfigApplier = registry is not null ? new LabConfigApplier(registry, _logger) : null;
    }

    public async Task<InstallationImageResult> ApplyAsync(
        string sourceIsoPath, GenerationWorkspace workspace, InstallationOptionsModel options,
        AutounattendConfiguration accountConfig, CancellationToken cancellationToken = default,
        IProgress<InstallationProgressInfo>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIsoPath);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(accountConfig);

        const string preparingStage = "Preparando workspace";
        const string bootWimPrepStage = "Preparando boot.wim";
        const string oobeStage = "Configurando OOBE";
        const string finalizingStage = "Finalizando";

        _logger.Info(
            $"[WORKSPACE] Phase=install-options-inicio WorkspaceId={workspace.WorkspaceId} " +
            $"SourceIsoPath={sourceIsoPath} BootWimPath={workspace.BootWimPath}");
        progress?.Report(InstallationProgressInfo.Create(preparingStage, 2, "Validando configuración de instalación..."));

        // Aborta antes de tocar nada si hay una opción sin mecanismo implementado
        // (P16, sección 9): nunca generar algo aparentemente completo pero engañoso.
        var validation = InstallationExecutionValidator.Validate(options);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors);
            _logger.Error(message);
            progress?.Report(InstallationProgressInfo.Create(preparingStage, 2, message, InstallationProgressLevel.Error));
            return new InstallationImageResult(false, Array.Empty<string>(), validation.Errors, null);
        }

        progress?.Report(InstallationProgressInfo.Create(preparingStage, 10, "Configuración validada", InstallationProgressLevel.Success));

        progress?.Report(InstallationProgressInfo.Create(bootWimPrepStage, 12, "Comprobando copia de boot.wim en el workspace..."));
        await _bootWimProvisioner.EnsureBootWimCopyAsync(sourceIsoPath, workspace.BootWimPath, cancellationToken)
            .ConfigureAwait(false);
        progress?.Report(InstallationProgressInfo.Create(bootWimPrepStage, 20, "boot.wim listo en el workspace", InstallationProgressLevel.Success));

        // P28: LabConfig se aplica y se verifica en AMBOS índices (1 = WinPE,
        // 2 = Windows Setup); el primer índice que falle aborta sin tocar el
        // siguiente, para no dejar boot.wim modificado a medias entre índices.
        var errors = new List<string>();
        var applied = new List<string>();
        var allIndicesSucceeded = true;

        foreach (var index in BootWimIndicesRequiringLabConfig)
        {
            var applyOfflineOobeBypass = index == WindowsSetupBootWimIndex && options.AllowOfflineOobe;

            var indexModification = await _bootWimModifier
                .ApplyLabConfigAsync(workspace.BootWimPath, index, options, workspace.MountPath, cancellationToken, progress, applyOfflineOobeBypass)
                .ConfigureAwait(false);

            applied.AddRange(indexModification.AppliedLogLines);
            errors.AddRange(indexModification.Errors);

            if (!indexModification.Success)
            {
                allIndicesSucceeded = false;
                _logger.Error($"[COMPAT] boot.wim index {index}: LabConfig modification failed.");
                break;
            }

            _logger.Info($"[COMPAT] boot.wim index {index}: LabConfig applied and verified.");
        }

        var modification = new BootWimModificationResult(allIndicesSucceeded, applied, errors);
        string? autounattendPath = null;

        if (modification.Success)
        {
            var autounattendFilePath = Path.Combine(workspace.WorkspacePath, "autounattend.xml");

            if (options.AllowLocalAccount)
            {
                progress?.Report(InstallationProgressInfo.Create(oobeStage, 92, "Generando autounattend.xml..."));
                try
                {
                    // Determinista: siempre sobrescribe el mismo archivo, nunca duplica.
                    var xml = AutounattendGenerator.Generate(accountConfig, options.AllowOfflineOobe);
                    File.WriteAllText(autounattendFilePath, xml);
                    autounattendPath = autounattendFilePath;

                    const string line = "[INSTALL] Local account enabled";
                    applied.Add(line);
                    _logger.Info(line);
                    progress?.Report(InstallationProgressInfo.Create(oobeStage, 96, "autounattend.xml generado", InstallationProgressLevel.Success));
                }
                catch (IsoEngineException ex)
                {
                    errors.Add(ex.Message);
                }
            }
            else if (File.Exists(autounattendFilePath))
            {
                // Cuenta local desactivada: no debe quedar un autounattend.xml de una
                // ejecución anterior imponiendo una cuenta local no deseada.
                File.Delete(autounattendFilePath);
                _logger.Info("autounattend.xml eliminado (cuenta local desactivada).");
            }
        }

        var success = modification.Success && errors.Count == 0;
        progress?.Report(InstallationProgressInfo.Create(
            finalizingStage, 100,
            success ? "Configuración de instalación aplicada" : "La configuración de instalación no se completó",
            success ? InstallationProgressLevel.Success : InstallationProgressLevel.Error));

        _logger.Info($"[WORKSPACE] Phase=install-options-fin WorkspaceId={workspace.WorkspaceId} Success={success}");

        return new InstallationImageResult(success, applied, errors, autounattendPath);
    }

    // P29: índice de install.wim en la copia final del workspace de generación.
    // WorkingImageFactory.CreateAsync (Export-Image) siempre exporta a índice 1
    // -- distinto de workspace.Index, que es el índice ORIGINAL dentro del
    // install.wim multi-edición de la ISO fuente.
    private const int FinalInstallWimIndex = 1;

    public async Task<WorkspaceValidationResult> ValidateFinalAsync(
        GenerationWorkspace workspace, InstallationOptionsModel options, AutounattendConfiguration accountConfig,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(accountConfig);

        if (_dism is null || _registry is null || _labConfigApplier is null)
        {
            _logger.Warn("[VALIDATE] InstallationImageService se construyó sin IDismRunner/IOfflineRegistryEditor; se omite la validación final de boot.wim/install.wim/autounattend (P28/P29).");
            return WorkspaceValidationResult.Valid;
        }

        var errors = new List<string>();

        foreach (var index in BootWimIndicesRequiringLabConfig)
        {
            errors.AddRange(await ValidateBootWimIndexAsync(workspace.BootWimPath, index, options, workspace.MountPath, cancellationToken)
                .ConfigureAwait(false));
        }

        if (options.AllowLocalAccount)
            errors.AddRange(ValidateAutounattend(workspace, options, accountConfig));

        if (options.AllowOfflineOobe)
            errors.AddRange(await ValidateInstallWimOfflineOobeAsync(workspace, cancellationToken).ConfigureAwait(false));

        return errors.Count == 0 ? WorkspaceValidationResult.Valid : new WorkspaceValidationResult(false, errors);
    }

    /// <summary>
    /// Vuelve a montar boot.wim de SOLO LECTURA (independiente del mount RW ya
    /// comprometido y desmontado por <see cref="ApplyAsync"/>) y a leer el hive
    /// SYSTEM para confirmar los valores realmente presentes -- nunca se acepta
    /// que DISM/reg.exe terminaran sin error como prueba suficiente. Registra
    /// LabConfig y BypassNRO como dos confirmaciones [OK] independientes
    /// (P29, sección 8).
    /// </summary>
    private async Task<List<string>> ValidateBootWimIndexAsync(
        string bootWimPath, int index, InstallationOptionsModel options, string mountDir, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        var mount = await _dism!.MountWimAsync(bootWimPath, index, mountDir, readOnly: true, cancellationToken).ConfigureAwait(false);
        if (!mount.Succeeded)
        {
            errors.Add($"No se pudo montar boot.wim índice {index} para la validación final (ExitCode {mount.ExitCode}).");
            return errors;
        }

        var hiveKeyName = "MRS_ISOENGINE_FINALVALIDATE_" + Guid.NewGuid().ToString("N")[..8];
        var hiveFilePath = Path.Combine(mountDir, "Windows", "System32", "config", "SYSTEM");
        var hiveLoaded = false;

        try
        {
            var load = await _registry!.LoadHiveAsync(hiveKeyName, hiveFilePath, cancellationToken).ConfigureAwait(false);
            if (!load.Succeeded)
            {
                errors.Add($"No se pudo cargar el hive SYSTEM offline del índice {index} para la validación final (ExitCode {load.ExitCode}).");
                return errors;
            }

            hiveLoaded = true;

            var verification = await _labConfigApplier!.VerifyAsync(hiveKeyName, options, cancellationToken).ConfigureAwait(false);
            if (!verification.IsValid)
                errors.AddRange(verification.Errors.Select(e => $"[boot.wim índice {index}] {e}"));
            else
                _logger.Info($"[OK] boot.wim Index {index} LabConfig");

            if (index == WindowsSetupBootWimIndex && options.AllowOfflineOobe)
            {
                var oobeVerification = await _labConfigApplier.VerifyOfflineOobeBypassAsync(hiveKeyName, cancellationToken).ConfigureAwait(false);
                if (!oobeVerification.IsValid)
                    errors.AddRange(oobeVerification.Errors.Select(e => $"[boot.wim índice {index}] {e}"));
                else
                    _logger.Info($"[OK] boot.wim Index {index} BypassNRO");
            }
        }
        finally
        {
            if (hiveLoaded)
                await _registry!.UnloadHiveAsync(hiveKeyName, cancellationToken).ConfigureAwait(false);

            // Solo lectura: nunca se confirma nada aquí, siempre se descarta
            // (no hay ningún cambio que hacer persistente en una re-validación).
            await _dism.UnmountWimDiscardAsync(mountDir, cancellationToken).ConfigureAwait(false);
        }

        return errors;
    }

    /// <summary>
    /// P29: vuelve a montar el install.wim FINAL del workspace de generación
    /// (de solo lectura, índice 1 -- ver <see cref="FinalInstallWimIndex"/>) y
    /// relee BypassNRO del hive SYSTEM, para confirmar que
    /// <c>InstallWimOobeConfigurator</c> lo dejó realmente persistido -- una
    /// sesión de montaje completamente nueva, independiente de la que hizo el
    /// commit.
    /// </summary>
    private async Task<List<string>> ValidateInstallWimOfflineOobeAsync(GenerationWorkspace workspace, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (!File.Exists(workspace.InstallWimPath))
        {
            errors.Add("No se encuentra install.wim en el workspace para validar OOBE offline.");
            return errors;
        }

        var mount = await _dism!.MountWimAsync(workspace.InstallWimPath, FinalInstallWimIndex, workspace.MountPath, readOnly: true, cancellationToken)
            .ConfigureAwait(false);
        if (!mount.Succeeded)
        {
            errors.Add($"No se pudo montar install.wim (índice {FinalInstallWimIndex}) para validar OOBE offline (ExitCode {mount.ExitCode}).");
            return errors;
        }

        var hiveKeyName = "MRS_ISOENGINE_FINALVALIDATE_INSTALLWIM_" + Guid.NewGuid().ToString("N")[..8];
        var hiveFilePath = Path.Combine(workspace.MountPath, "Windows", "System32", "config", "SYSTEM");
        var hiveLoaded = false;

        try
        {
            var load = await _registry!.LoadHiveAsync(hiveKeyName, hiveFilePath, cancellationToken).ConfigureAwait(false);
            if (!load.Succeeded)
            {
                errors.Add($"No se pudo cargar el hive SYSTEM offline de install.wim para la validación final (ExitCode {load.ExitCode}).");
                return errors;
            }

            hiveLoaded = true;
            _logger.Info("[OK] install.wim OOBE configuration");

            var verification = await _labConfigApplier!.VerifyOfflineOobeBypassAsync(hiveKeyName, cancellationToken).ConfigureAwait(false);
            if (!verification.IsValid)
                errors.AddRange(verification.Errors.Select(e => $"[install.wim] {e}"));
            else
                _logger.Info("[OK] BypassNRO persisted");
        }
        finally
        {
            if (hiveLoaded)
                await _registry!.UnloadHiveAsync(hiveKeyName, cancellationToken).ConfigureAwait(false);

            await _dism.UnmountWimDiscardAsync(workspace.MountPath, cancellationToken).ConfigureAwait(false);
        }

        return errors;
    }

    // Únicos componentes reales que este generador escribe (P16/P28/P29); si
    // apareciera cualquier otro nombre de componente, es señal de un XML ajeno
    // o corrupto, no de un autounattend generado por MRS.
    private static readonly string[] KnownComponentNames =
    {
        "Microsoft-Windows-Setup", "Microsoft-Windows-Deployment", "Microsoft-Windows-Shell-Setup",
    };

    /// <summary>
    /// Confirma, leyendo el XML ya escrito por <see cref="ApplyAsync"/>, que
    /// contiene una fase windowsPE válida (P29), una cuenta local, y (si aplica)
    /// el comando de BypassNRO en specialize -- nunca se acepta que
    /// <c>File.Exists</c> por sí solo sea prueba de que el contenido es correcto.
    /// </summary>
    private List<string> ValidateAutounattend(GenerationWorkspace workspace, InstallationOptionsModel options, AutounattendConfiguration accountConfig)
    {
        var errors = new List<string>();
        var path = Path.Combine(workspace.WorkspacePath, "autounattend.xml");

        if (!File.Exists(path))
        {
            errors.Add("No se encuentra autounattend.xml en el workspace.");
            return errors;
        }

        _logger.Info("[OK] autounattend.xml found");

        XDocument document;
        try
        {
            document = XDocument.Load(path);
        }
        catch (Exception ex)
        {
            errors.Add($"autounattend.xml no es un XML válido: {ex.Message}");
            return errors;
        }

        _logger.Info("[OK] autounattend.xml valid");

        XNamespace ns = "urn:schemas-microsoft-com:unattend";

        var passes = document.Root?.Elements(ns + "settings")
            .Select(e => (string?)e.Attribute("pass"))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        if (!passes.Contains("windowsPE"))
        {
            errors.Add("autounattend.xml no contiene una fase windowsPE.");
        }
        else
        {
            _logger.Info("[OK] windowsPE configured");
        }

        if (!passes.Contains("oobeSystem"))
        {
            errors.Add("autounattend.xml no contiene una fase oobeSystem.");
        }
        else
        {
            _logger.Info("[OK] oobeSystem configured");
        }

        // "componentes válidos": todo <component name="..."> debe ser uno de los
        // que MRS realmente escribe -- nunca un nombre inventado o corrupto.
        var unknownComponents = document.Descendants(ns + "component")
            .Select(e => (string?)e.Attribute("name"))
            .Where(name => name is not null && !KnownComponentNames.Contains(name))
            .ToList();
        if (unknownComponents.Count > 0)
            errors.Add($"autounattend.xml contiene componentes no reconocidos: {string.Join(", ", unknownComponents)}.");

        // P30: comprueba que el LocalAccount existe Y que su nombre coincide con
        // el realmente configurado -- nunca basta con "existe alguna cuenta".
        var localAccount = document.Descendants(ns + "LocalAccount").FirstOrDefault();
        if (localAccount is null)
        {
            errors.Add("autounattend.xml no contiene una cuenta local configurada.");
        }
        else
        {
            var configuredName = localAccount.Element(ns + "Name")?.Value;
            if (!string.Equals(configuredName, accountConfig.AccountName, StringComparison.Ordinal))
                errors.Add($"autounattend.xml define la cuenta local '{configuredName}', pero se configuró '{accountConfig.AccountName}'.");
            else
                _logger.Info($"[OK] LocalAccount: {accountConfig.AccountName}");
        }

        // P30: Generate() ahora SIEMPRE escribe <Password> (con Value vacío si no
        // se pidió contraseña -- ver AutounattendGenerator, evita que Windows
        // fuerce un cambio de contraseña en el primer inicio de sesión), así que
        // ya no basta con comprobar si el elemento existe: hay que comparar el
        // contenido (vacío/no vacío) con lo realmente configurado. El valor en sí
        // NUNCA se registra en el log, solo si está "configured" o "empty".
        var expectedHasPassword = !string.IsNullOrEmpty(accountConfig.Password);
        var passwordElement = localAccount?.Element(ns + "Password");
        var actualPasswordValue = passwordElement?.Element(ns + "Value")?.Value;
        var actualHasPassword = !string.IsNullOrEmpty(actualPasswordValue);

        if (passwordElement is null)
            errors.Add("autounattend.xml no contiene el elemento Password para la cuenta local.");
        else if (expectedHasPassword && !actualHasPassword)
            errors.Add("autounattend.xml no contiene la contraseña configurada.");
        else if (!expectedHasPassword && actualHasPassword)
            errors.Add("autounattend.xml contiene una contraseña pese a que no se proporcionó ninguna.");
        else
            _logger.Info(expectedHasPassword ? "[OK] LocalAccount password: configured" : "[OK] LocalAccount password: empty");

        // "Cuenta Microsoft no requerida": HideOnlineAccountScreens evita que
        // Setup ofrezca el flujo de cuenta Microsoft durante oobeSystem.
        var hideOnlineAccounts = document.Descendants(ns + "HideOnlineAccountScreens").FirstOrDefault()?.Value;
        if (!string.Equals(hideOnlineAccounts, "true", StringComparison.OrdinalIgnoreCase))
            errors.Add("autounattend.xml no oculta las pantallas de cuenta en línea (HideOnlineAccountScreens).");

        if (options.AllowOfflineOobe)
        {
            var hasBypassNroCommand = document.Descendants(ns + "Path")
                .Any(e => e.Value.Contains("BypassNRO", StringComparison.OrdinalIgnoreCase));

            if (!hasBypassNroCommand)
                errors.Add("autounattend.xml no contiene el comando de bypass de red offline (BypassNRO) esperado en specialize.");
        }

        return errors;
    }
}
