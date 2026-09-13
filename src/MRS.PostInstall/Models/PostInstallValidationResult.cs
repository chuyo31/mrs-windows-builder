namespace MRS.PostInstall.Models;

/// <summary>Resultado de validar una <see cref="PostInstallConfiguration"/>/<see cref="PostInstallSourceFiles"/> antes de generar el paquete (P18).</summary>
public sealed record PostInstallValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static PostInstallValidationResult Valid { get; } = new(true, Array.Empty<string>());
}
