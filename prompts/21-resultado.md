# Resultado - Corrección: crash de arranque en MRS.WindowsBuilder.exe

## Causa exacta

`MainWindow.xaml.cs`, línea 944, dentro de `InstallationOption_Changed`
(`src/MRS.WindowsBuilder/MainWindow.xaml.cs`). Varias casillas del panel
"OPCIONES DE INSTALACIÓN" fijan `IsChecked="True"` directamente en el XAML
(`AllowLocalAccountCheckBox`, `AllowOfflineOobeCheckBox`, `BypassTpmCheckBox`,
`BypassSecureBootCheckBox`, `BypassCpuCheckBox`, `BypassRamCheckBox` —
`src/MRS.WindowsBuilder/MainWindow.xaml`, líneas 992-1019) y tienen el evento
`Checked` conectado ya en ese mismo elemento XAML
(`Checked="InstallationOption_Changed"`).

Cuando WPF construye el árbol visual dentro de `InitializeComponent()`, en
cuanto procesa `AllowLocalAccountCheckBox` (el primero con `IsChecked="True"`)
dispara inmediatamente su evento `Checked`, invocando
`InstallationOption_Changed` — pero en ese instante los `CheckBox` con
`x:Name` declarados **después** en el documento XAML (`AllowOfflineOobeCheckBox`,
`BypassTpmCheckBox`, `BypassSecureBootCheckBox`, `BypassCpuCheckBox`,
`BypassRamCheckBox`) todavía no están conectados a sus campos generados: esos
campos siguen valiendo `null`. El cuerpo del método (línea 944 en adelante)
lee, entre otros, `BypassRamCheckBox.IsChecked`, y esa referencia nula
produce el `NullReferenceException`.

(`_installationOptions`, pese a ser el campo actualizado en esa misma línea,
**no** es la causa: su inicializador de campo —
`InstallationOptionsModel.Default with { BypassStorage = false }`, línea 102
— ya se ejecuta antes de que el constructor llame a `InitializeComponent()`,
así que nunca es null en este punto.)

`SecurityOption_Changed` tiene el mismo patrón en el XAML (`PreKeepDefenderCheckBox`/
`PreKeepWindowsUpdateCheckBox` con `IsChecked="True"` y el evento ya conectado,
línea 575-586) pero no falla porque ya tiene una guarda temprana no
relacionada con la inicialización de la ventana:
`if (_activeCatalogProfileId is null || _profileLoadResult is null || ...) return;`
— al arrancar, todavía no hay ningún perfil de catálogo activo, así que esa
guarda ya corta el método antes de tocar ningún `CheckBox`. `InstallationOption_Changed`
no tenía ninguna guarda equivalente porque, por diseño (P15), esta
configuración es independiente del catálogo/perfil — no existe ningún campo
de ese tipo con el que protegerse de forma natural.

## Cambio realizado

Un único guard al principio de `InstallationOption_Changed`
(`src/MRS.WindowsBuilder/MainWindow.xaml.cs`):

```csharp
private void InstallationOption_Changed(object sender, RoutedEventArgs e)
{
    // Varias casillas fijan IsChecked="True" directamente en el XAML, lo que
    // dispara este evento durante InitializeComponent(), antes de que los
    // demás CheckBox con x:Name de este panel estén conectados a sus campos
    // (siguen siendo null en ese momento) -> NullReferenceException. La
    // ventana (this) no queda IsInitialized hasta que EndInit() se completa
    // al final de InitializeComponent(), así que basta con salir aquí; una
    // vez inicializada la ventana, el comportamiento es el de siempre.
    if (!IsInitialized)
        return;

    _installationOptions = _installationOptions with
    {
        ...
    };
}
```

No se ha tocado `MainWindow.xaml`, ni `InstallationOptions`, `ISOEngine`,
`RemovalEngine`, `ProfileEngine` ni `ComponentCatalog`. No se ha modificado
ningún evento XAML ni se ha eliminado ningún `Checked`/`Unchecked`.

## Por qué el cambio es seguro durante InitializeComponent

`FrameworkElement.IsInitialized` (heredado por `Window`) solo pasa a `true`
cuando se completa `EndInit()` sobre ese objeto, y para el objeto raíz de
una ventana WPF eso ocurre al **final** de `InitializeComponent()`, después
de que todo el árbol de elementos hijos (incluidos todos los `CheckBox` del
panel) se haya construido y conectado. Por tanto:

- Mientras `InitializeComponent()` está construyendo el árbol (incluido el
  instante en que `AllowLocalAccountCheckBox` dispara su `Checked` inicial),
  `this.IsInitialized` vale `false` — el guard corta ahí, sin leer ningún
  campo todavía no conectado.
- En cuanto `InitializeComponent()` termina, `this.IsInitialized` pasa a
  `true` para siempre (una `Window` no vuelve a `BeginInit()`), así que
  **todas** las interacciones reales del usuario con estas casillas —así
  como las llamadas posteriores de `RefreshInstallationOptionsCheckboxes()`,
  que ya desconecta/reconecta los manejadores antes de tocar `IsChecked` —
  siguen ejecutando exactamente la misma lógica que antes del cambio.

No depende de ningún campo nuevo ni de un orden concreto de inicialización
además del que WPF ya garantiza (`EndInit` del elemento raíz al final de
`InitializeComponent`), así que no introduce ningún nuevo caso frágil.

## Verificación manual (además de los tests)

Se compiló y se ejecutó `MRS.WindowsBuilder.exe` directamente: antes del
cambio la ventana se cerraba de inmediato (el crash reportado); con el
cambio aplicado, el proceso permanece en ejecución con la ventana abierta
(confirmado con `Start-Process`/`Get-Process` unos segundos después de
arrancar).

## Test añadido

No se ha añadido ningún test automático. El propio código (constructores de
`CheckBox`, `RoutedEventArgs`, orden de conexión de campos `x:Name` durante
`InitializeComponent()`) requiere el runtime real de WPF para reproducir el
escenario exacto del bug (el orden de conexión de campos generado por el
compilador XAML no es algo que se pueda simular con un `CheckBox` construido
a mano en un test); no existe ningún proyecto de test para
`MRS.WindowsBuilder` en el repositorio (todas las suites existentes cubren
los motores, nunca la UI, una decisión ya documentada en fases anteriores).
Añadir aquí la primera dependencia de WPF en la suite de tests solo para
este guard habría sido una dependencia innecesaria, tal como el propio
prompt pide evitar. En su lugar, la corrección se verificó ejecutando el
`.exe` real (ver arriba).

## Resultado de tests

```
dotnet test MRS-Windows-Builder.sln
```

```
MRS.InstallationOptions.Tests : 17/17
MRS.ProfileEngine.Tests       : 21/21
MRS.PostInstall.Tests         : 44/44
MRS.ImageEngine.Tests         : 78/78
MRS.ComponentCatalog.Tests    : 59/59
MRS.RemovalPlanning.Tests     : 48/48
MRS.RemovalEngine.Tests       : 48/48
MRS.ISOEngine.Tests           : 102/102
```

Total: **417/417**, 0 fallos. Sin cambios respecto al baseline de P20 (esta
corrección no toca ningún proyecto de motor, solo `MRS.WindowsBuilder`, que
no tiene tests).

## Resultado del build

```
dotnet build MRS-Windows-Builder.sln
Compilación correcta. 0 Advertencia(s). 0 Errores.
```

## Commit recomendado

`git commit` con el resumen "Fix: MRS.WindowsBuilder.exe cerraba al arrancar
— InstallationOption_Changed leía CheckBox todavía no conectados durante
InitializeComponent (NullReferenceException en MainWindow.xaml.cs:944);
guard con IsInitialized, sin cambios funcionales una vez arrancada la
ventana; 417/417 sin regresiones" y `git push` a `main`.
