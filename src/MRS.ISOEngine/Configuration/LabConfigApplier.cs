using MRS.DismEngine.Logging;
using MRS.ISOEngine.Models;
using MRS.ISOEngine.Registry;
// Ver InstallationConfigurationPlanner.cs: mismo caso de colisión namespace/tipo.
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Configuration;

/// <summary>
/// Aplica/retira los 4 bypasses de compatibilidad (P16, sección 3) bajo
/// <c>HKLM\SYSTEM\Setup\LabConfig</c> del hive offline ya cargado por
/// <c>BootWimModifier</c>. Solo aplica las opciones activadas; una opción
/// desactivada se elimina explícitamente si existía de una ejecución anterior
/// (idempotencia — sección 11: "opciones desactivadas no aparecen").
///
/// <see cref="ApplyOfflineOobeBypassAsync"/> aplica BypassNRO (P28): se invoca
/// explícitamente desde <see cref="BootWim.BootWimModifier"/> solo sobre el
/// índice de boot.wim que corresponde a Windows Setup (índice 2, confirmado
/// con auditoría real de un ISO generado por MRS — ver prompts/28-resultado.md),
/// nunca automáticamente para todos los índices ni desde <see cref="ApplyAsync"/>.
/// </summary>
public sealed class LabConfigApplier
{
    public const string LabConfigSubKeyPath = "Setup\\LabConfig";
    public const string OobeSubKeyPath = "Setup\\OOBE";
    public const string BypassNroValueName = "BypassNRO";

    private static readonly (string ValueName, Func<InstallationOptionsModel, bool> IsEnabled, string LogSuffix)[] CompatBypasses =
    {
        ("BypassTPMCheck", o => o.BypassTpm, "BypassTPM applied"),
        ("BypassSecureBootCheck", o => o.BypassSecureBoot, "BypassSecureBoot applied"),
        ("BypassCPUCheck", o => o.BypassCpu, "BypassCPU applied"),
        ("BypassRAMCheck", o => o.BypassRam, "BypassRAM applied"),
    };

    private readonly IOfflineRegistryEditor _registry;
    private readonly IAppLogger _logger;

    public LabConfigApplier(IOfflineRegistryEditor registry, IAppLogger? logger = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? NullAppLogger.Instance;
    }

    /// <summary>
    /// Aplica los 4 bypasses activados y elimina los desactivados. Devuelve las
    /// líneas de log de las opciones realmente aplicadas (nunca de las omitidas).
    /// </summary>
    public async Task<IReadOnlyList<string>> ApplyAsync(
        string hiveKeyName, InstallationOptionsModel options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hiveKeyName);
        ArgumentNullException.ThrowIfNull(options);

        var applied = new List<string>();

        foreach (var (valueName, isEnabled, logSuffix) in CompatBypasses)
        {
            if (isEnabled(options))
            {
                var result = await _registry.SetDwordAsync(hiveKeyName, LabConfigSubKeyPath, valueName, 1, cancellationToken)
                    .ConfigureAwait(false);
                if (!result.Succeeded)
                    throw new Exceptions.IsoEngineException($"No se pudo aplicar {valueName} (ExitCode {result.ExitCode}).", result.ExitCode);

                var line = $"[COMPAT] {logSuffix}";
                applied.Add(line);
                _logger.Info(line);
            }
            else
            {
                // No crear automáticamente opciones que el usuario haya desactivado: si de una
                // ejecución anterior quedó el valor puesto, se retira (idempotencia).
                await _registry.DeleteValueAsync(hiveKeyName, LabConfigSubKeyPath, valueName, cancellationToken)
                    .ConfigureAwait(false);
                // reg delete devuelve ExitCode != 0 si el valor no existía: no es un error real,
                // así que deliberadamente no se comprueba Succeeded aquí.
            }
        }

        return applied;
    }

    /// <summary>
    /// Aplica <c>HKLM\SYSTEM\Setup\OOBE\BypassNRO=1</c> (P28): el mismo valor que
    /// deja <c>OOBE\BYPASSNRO.cmd</c> al ejecutarse manualmente durante OOBE, pero
    /// aplicado offline antes de que Setup arranque siquiera — así no depende de
    /// ninguna intervención manual. Se invoca únicamente sobre el índice de
    /// boot.wim de Windows Setup (nunca sobre WinPE ni automáticamente para todos
    /// los índices).
    /// </summary>
    public async Task<bool> ApplyOfflineOobeBypassAsync(string hiveKeyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hiveKeyName);

        var result = await _registry.SetDwordAsync(hiveKeyName, OobeSubKeyPath, BypassNroValueName, 1, cancellationToken)
            .ConfigureAwait(false);

        if (result.Succeeded)
            _logger.Info("[COMPAT] BypassNRO applied");

        return result.Succeeded;
    }

    /// <summary>
    /// Lee, con el hive ya cargado, que <c>BypassNRO</c> quedó realmente presente
    /// (P28): igual que <see cref="VerifyAsync"/>, nunca se acepta como éxito que
    /// DISM/reg.exe terminaran sin error — se confirma leyendo el valor de vuelta.
    /// </summary>
    public async Task<Models.WorkspaceValidationResult> VerifyOfflineOobeBypassAsync(
        string hiveKeyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hiveKeyName);

        var query = await _registry.QueryValueAsync(hiveKeyName, OobeSubKeyPath, BypassNroValueName, cancellationToken)
            .ConfigureAwait(false);

        if (!query.Succeeded)
            return new Models.WorkspaceValidationResult(
                false, new[] { "BypassNRO debería estar aplicado pero no se encontró en el registro offline." });

        _logger.Info("[COMPAT] BypassNRO verified");
        return Models.WorkspaceValidationResult.Valid;
    }

    /// <summary>
    /// Comprueba, leyendo el hive ya cargado, que cada bypass activado tiene el
    /// valor esperado y que ninguno desactivado sigue presente (P16, sección 12).
    /// </summary>
    public async Task<Models.WorkspaceValidationResult> VerifyAsync(
        string hiveKeyName, InstallationOptionsModel options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hiveKeyName);
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();

        foreach (var (valueName, isEnabled, _) in CompatBypasses)
        {
            var query = await _registry.QueryValueAsync(hiveKeyName, LabConfigSubKeyPath, valueName, cancellationToken)
                .ConfigureAwait(false);
            var exists = query.Succeeded;

            if (isEnabled(options) && !exists)
                errors.Add($"{valueName} debería estar aplicado pero no se encontró en el registro offline.");
            else if (!isEnabled(options) && exists)
                errors.Add($"{valueName} está desactivado en InstallationOptions pero sigue presente en el registro offline.");
        }

        if (errors.Count == 0)
            _logger.Info("[COMPAT] LabConfig verified");

        return errors.Count == 0
            ? Models.WorkspaceValidationResult.Valid
            : new Models.WorkspaceValidationResult(false, errors);
    }
}
