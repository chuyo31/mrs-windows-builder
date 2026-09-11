namespace MRS.RemovalPlanning.Models;

/// <summary>Aviso sobre un componente o sobre el plan en general.</summary>
public sealed record RemovalWarning(
    string Code,
    string Message,
    RemovalWarningSeverity Severity,
    string? ComponentId);
