namespace MRS.ISOEngine.BootWim;

/// <summary>
/// Garantiza que el workspace tenga su propia copia de <c>sources\boot.wim</c>
/// (P16, sección 2): monta la ISO original en solo lectura, copia el archivo al
/// workspace, y desmonta la ISO — nunca escribe en la ruta de la ISO original.
/// Idempotente: si el destino ya existe, no vuelve a copiar.
/// </summary>
public interface IBootWimProvisioner
{
    /// <summary>Devuelve <c>true</c> si tuvo que copiar el archivo; <c>false</c> si ya existía (idempotente).</summary>
    Task<bool> EnsureBootWimCopyAsync(string sourceIsoPath, string destinationBootWimPath, CancellationToken cancellationToken = default);
}
