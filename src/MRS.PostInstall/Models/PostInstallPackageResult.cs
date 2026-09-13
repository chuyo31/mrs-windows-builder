namespace MRS.PostInstall.Models;

/// <summary>
/// Resultado de <c>PostInstallPackageBuilder.BuildAsync</c> (P18). <see cref="OemRootPath"/>
/// es la carpeta <c>$OEM$</c> generada, lista para que una fase posterior de
/// ISOEngine la copie a <c>&lt;workspace&gt;\sources\$OEM$\</c> — o <c>null</c> si
/// PostInstall estaba deshabilitado o la validación falló.
/// </summary>
public sealed record PostInstallPackageResult(bool Success, string? OemRootPath, IReadOnlyList<string> Errors);
