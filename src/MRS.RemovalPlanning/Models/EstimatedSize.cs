namespace MRS.RemovalPlanning.Models;

/// <summary>
/// Tamaño estimado de liberar un componente. DISM no nos da tamaños reales por
/// paquete/feature/capability, así que <see cref="Bytes"/> es <c>null</c>
/// ("desconocido") salvo que un <c>SizeAnalyzer</c> futuro lo calcule. Nunca se
/// inventa un número.
/// </summary>
public sealed record EstimatedSize(long? Bytes)
{
    public static readonly EstimatedSize Unknown = new((long?)null);

    public bool IsKnown => Bytes.HasValue;

    public string Display => Bytes switch
    {
        null => "Tamaño estimado: desconocido",
        var b => $"Tamaño estimado: {b.Value:N0} bytes",
    };
}
