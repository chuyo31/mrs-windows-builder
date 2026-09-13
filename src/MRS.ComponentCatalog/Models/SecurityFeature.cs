namespace MRS.ComponentCatalog.Models;

/// <summary>
/// Identifica qué característica de seguridad protege una <see cref="MRS.ComponentCatalog.Rules.ProtectionRule"/>
/// concreta (P13). Solo existen dos: el resto de protecciones (Servicing Stack,
/// CBS, LCU, WinRE, OOBE, frameworks, red, USB, audio, impresión, etc.) no
/// llevan ninguno de estos valores y por tanto nunca se ven afectadas por
/// <see cref="SecurityOptions"/>.
/// </summary>
public enum SecurityFeature
{
    Defender,
    WindowsUpdate,
}
