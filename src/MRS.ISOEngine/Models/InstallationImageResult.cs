namespace MRS.ISOEngine.Models;

/// <summary>Resultado de <c>IInstallationImageService.ApplyAsync</c> (P16): qué se aplicó, qué falló, y dónde quedó el autounattend.xml generado (si se generó).</summary>
public sealed record InstallationImageResult(
    bool Success,
    IReadOnlyList<string> AppliedLogLines,
    IReadOnlyList<string> Errors,
    string? AutounattendPath);
