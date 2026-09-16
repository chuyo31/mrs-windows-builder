namespace MRS.ISOEngine.TreeCopy;

/// <summary>
/// Copia el árbol completo de una ISO montada (bootmgr, boot\, efi\, sources\,
/// setup.exe, etc.) a un workspace de generación (P19, "PREPARACIÓN"). Es el
/// paso que faltaba en P15/P16/P18: aquellas fases solo copiaban archivos WIM
/// concretos; sin el resto del árbol (en particular los cargadores de arranque
/// BIOS/UEFI bajo <c>boot\</c> y <c>efi\</c>) <c>oscdimg</c> no podría producir
/// una ISO arrancable. La ISO original se monta siempre en solo lectura.
/// </summary>
public interface IIsoTreeCopier
{
    /// <summary>Copia todo el contenido de la ISO a <paramref name="destinationRoot"/>. No sobrescribe archivos que el llamador vaya a reemplazar después (boot.wim/install.wim modificados, autounattend.xml, $OEM$) — simplemente los deja como estaban en el origen; pasos posteriores del pipeline los sustituyen con <c>overwrite: true</c>.</summary>
    Task CopyAsync(string sourceIsoPath, string destinationRoot, CancellationToken cancellationToken = default);
}
