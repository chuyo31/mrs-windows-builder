namespace MRS.PostInstall.Models;

/// <summary>
/// Telemetría de progreso para <c>PostInstallPackageBuilder</c> (P18). Mismo
/// patrón que <c>MRS.RemovalEngine.Models.ProgressInfo</c> (P10) y
/// <c>MRS.ISOEngine.Models.InstallationProgressInfo</c> (P16) — Stage/Percent/
/// Message/Level/Timestamp — pero como tipo propio: MRS.PostInstall no
/// referencia esos proyectos (mismo principio de independencia entre motores
/// ya aplicado repetidamente en fases anteriores).
/// </summary>
public sealed record PostInstallProgressInfo(
    string Stage, int Percent, string Message, PostInstallProgressLevel Level, DateTimeOffset Timestamp)
{
    public static PostInstallProgressInfo Create(
        string stage, int percent, string message, PostInstallProgressLevel level = PostInstallProgressLevel.Info)
        => new(stage, Math.Clamp(percent, 0, 100), message, level, DateTimeOffset.Now);
}
