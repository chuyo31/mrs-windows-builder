namespace MRS.PostInstall.Models;

/// <summary>
/// Describe una ejecución de proceso de forma reutilizable (P18, sección 5):
/// executable/arguments/working directory/timeout/expected exit codes. Un
/// único lugar donde vive la línea de comandos exacta (p. ej. "/install
/// /quiet /norestart"), en vez de repetirla en varios sitios — ver
/// <c>PostInstallCommands</c>, que es quien construye estas specs.
/// </summary>
public sealed record CommandExecutionSpec
{
    public string Executable { get; init; } = string.Empty;

    public string Arguments { get; init; } = string.Empty;

    public string? WorkingDirectory { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Códigos de salida que cuentan como éxito. El proceso "terminó" nunca es, por sí solo, éxito.</summary>
    public IReadOnlyCollection<int> ExpectedExitCodes { get; init; } = new[] { 0 };
}
