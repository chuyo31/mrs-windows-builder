namespace MRS.ISOEngine.Models;

/// <summary>
/// Telemetría de progreso para <c>InstallationImageService</c> (P16). Mismo patrón
/// que <c>MRS.RemovalEngine.Models.ProgressInfo</c> (P10) — Stage/Percent/Message/
/// Level/Timestamp reportados vía <see cref="IProgress{T}"/> — pero como un tipo
/// propio: MRS.ISOEngine no referencia MRS.RemovalEngine (mismo principio de
/// independencia entre motores que ya se aplicó a SecurityOptions en P13/P15).
/// </summary>
public sealed record InstallationProgressInfo(
    string Stage, int Percent, string Message, InstallationProgressLevel Level, DateTimeOffset Timestamp)
{
    public static InstallationProgressInfo Create(
        string stage, int percent, string message, InstallationProgressLevel level = InstallationProgressLevel.Info)
        => new(stage, Math.Clamp(percent, 0, 100), message, level, DateTimeOffset.Now);
}
