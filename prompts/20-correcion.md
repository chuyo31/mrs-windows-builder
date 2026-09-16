# PROMPT — FIX WPF STARTUP INSTALLATION OPTIONS

Proyecto:
C:\Users\B3RASCASA\Desktop\MRS-W11PRO-by_chuyo31

Problema:
MRS.WindowsBuilder.exe se cierra inmediatamente al arrancar.

Excepción real ejecutando:
dotnet MRS.WindowsBuilder.dll

System.NullReferenceException:
MRS.WindowsBuilder.MainWindow.InstallationOption_Changed(...)
MainWindow.xaml.cs: line 944

La excepción ocurre durante:
MainWindow.InitializeComponent()

Por tanto, uno de los CheckBox de opciones de instalación está disparando
InstallationOption_Changed durante la construcción XAML, antes de que el estado
necesario del MainWindow esté inicializado.

OBJETIVO:
Corregir únicamente este problema de inicialización WPF.

REQUISITOS:
1. Inspecciona MainWindow.xaml.cs y MainWindow.xaml.
2. Localiza exactamente qué referencia es null en la línea 944.
3. Haz que InstallationOption_Changed sea seguro durante InitializeComponent:
   - no debe ejecutar lógica que dependa de campos todavía no inicializados;
   - puede salir inmediatamente si la ventana/estado necesario todavía no está listo.
4. NO cambies la lógica funcional de InstallationOptions.
5. NO cambies InstallationOptions, ISOEngine, RemovalEngine, ProfileEngine,
   ComponentCatalog ni el pipeline de generación.
6. NO elimines los eventos XAML como solución rápida.
7. Mantén el comportamiento actual una vez que la ventana esté completamente
   inicializada.
8. Si existe un patrón de inicialización equivalente en SecurityOption_Changed,
   úsalo solo como referencia, sin duplicar código innecesariamente.
9. Añade un test si es posible sin introducir dependencia WPF innecesaria.
10. Ejecuta:
    dotnet test
    dotnet build
11. Ambos deben terminar correctamente.
12. Crea:
    prompts\21-resultado.md

En el resultado documenta:
- causa exacta del NullReferenceException;
- archivo y línea;
- cambio realizado;
- por qué el cambio es seguro durante InitializeComponent;
- resultado de tests;
- resultado del build.

No hagas ninguna otra modificación ni refactorización.

Al finalizar, recomienda el commit correspondiente.