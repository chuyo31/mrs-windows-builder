using System.Text;
using MRS.PostInstall.Execution;
using MRS.PostInstall.Models;

namespace MRS.PostInstall.Scripts;

/// <summary>
/// Genera <c>SetupComplete.cmd</c> (P18, sección 9/16): el script que Windows
/// Setup ejecuta automáticamente, una sola vez, en contexto SYSTEM, al final de
/// la instalación — antes de que exista ninguna sesión de usuario interactiva.
/// Ver "Decisión del mecanismo" en <c>prompts/18-resultado.md</c> para la
/// comparación completa con <c>$OEM$\$$\Setup\Scripts</c> (es la misma técnica:
/// esta es la ubicación de origen que Setup copia a
/// <c>%WinDir%\Setup\Scripts\SetupComplete.cmd</c>), FirstLogonCommands y RunOnce.
///
/// Determinista: la misma <see cref="PostInstallConfiguration"/> produce siempre
/// el mismo texto. Solo usa rutas relativas a su propia ubicación (<c>%~dp0</c>)
/// — nunca <c>C:\Users\...</c> ni ninguna ruta del equipo de desarrollo — porque
/// no puede saber, ni necesita saber, en qué unidad ni bajo qué usuario se
/// instalará Windows.
/// </summary>
public static class SetupCompleteScriptGenerator
{
    public static string Generate(PostInstallConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Las banderas reales ("/install /quiet /norestart") viven en un único
        // sitio (PostInstallCommands); aquí solo se referencian, nunca se repiten.
        var dotNetArguments = PostInstallCommands.DotNetRuntimeInstall(config, "%SCRIPT_DIR%dotnet").Arguments;

        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("setlocal");
        sb.AppendLine("set \"SCRIPT_DIR=%~dp0\"");
        sb.AppendLine("set \"LOG=%~dp0PostInstall.log\"");
        sb.AppendLine();
        // Las líneas "[POSTINSTALL] Preparing package/Runtime/PCPI/Validation/Package
        // prepared successfully" son del BUILDER (tiempo de construcción del paquete,
        // registradas por PostInstallPackageBuilder vía IAppLogger); este script solo
        // registra lo que ocurre en tiempo de ejecución futura, en el equipo instalado.
        sb.AppendLine("echo [POSTINSTALL] Installing .NET>> \"%LOG%\"");
        sb.AppendLine($"\"%SCRIPT_DIR%dotnet\\{config.DotNetInstallerFileName}\" {dotNetArguments}");
        sb.AppendLine("set DOTNET_EXITCODE=%ERRORLEVEL%");
        sb.AppendLine("echo [POSTINSTALL] .NET ExitCode: %DOTNET_EXITCODE%>> \"%LOG%\"");
        sb.AppendLine();
        sb.AppendLine("if %DOTNET_EXITCODE% NEQ 0 (");
        sb.AppendLine("  echo [POSTINSTALL] .NET installation failed>> \"%LOG%\"");
        sb.AppendLine("  goto :EOF");
        sb.AppendLine(")");
        sb.AppendLine();
        sb.AppendLine("echo [POSTINSTALL] .NET installation completed>> \"%LOG%\"");

        if (config.RunPcpiAfterRuntime)
        {
            sb.AppendLine();
            sb.AppendLine("echo [POSTINSTALL] Launching PCPI>> \"%LOG%\"");
            sb.AppendLine($"\"%SCRIPT_DIR%pcpi\\{config.PcpiFileName}\"");
            sb.AppendLine("set PCPI_EXITCODE=%ERRORLEVEL%");
            sb.AppendLine("echo [POSTINSTALL] PCPI ExitCode: %PCPI_EXITCODE%>> \"%LOG%\"");
        }

        sb.AppendLine();
        sb.AppendLine("endlocal");

        return sb.ToString();
    }
}
