namespace MRS.ImageEngine.Iso;

/// <summary>
/// Monta temporalmente una ISO como unidad para poder leerla. El montaje es de
/// solo lectura y debe deshacerse siempre al liberar <see cref="IIsoMount"/>.
/// </summary>
public interface IIsoMounter
{
    Task<IIsoMount> MountAsync(string isoPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Montaje activo de una ISO. Al liberarlo se desmonta la imagen.
/// </summary>
public interface IIsoMount : IAsyncDisposable
{
    /// <summary>Raíz de la unidad montada, p. ej. <c>E:\</c>.</summary>
    string RootPath { get; }
}
