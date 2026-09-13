namespace MRS.ISOEngine.Models;

/// <summary>Resultado de validar una <see cref="AutounattendConfiguration"/> antes de generar el XML (P16).</summary>
public sealed record AutounattendValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static AutounattendValidationResult Valid { get; } = new(true, Array.Empty<string>());
}
