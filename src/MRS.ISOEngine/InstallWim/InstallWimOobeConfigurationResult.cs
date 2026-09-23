namespace MRS.ISOEngine.InstallWim;

/// <summary>Resultado de configurar BypassNRO offline sobre install.wim (P29).</summary>
public sealed record InstallWimOobeConfigurationResult(
    bool Success,
    IReadOnlyList<string> AppliedLogLines,
    IReadOnlyList<string> Errors);
