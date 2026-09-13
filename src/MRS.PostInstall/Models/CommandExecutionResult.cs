namespace MRS.PostInstall.Models;

/// <summary>Resultado de ejecutar un <see cref="CommandExecutionSpec"/>. <see cref="Success"/> se decide solo por <see cref="ExitCode"/> (contra <c>ExpectedExitCodes</c>), nunca porque "el proceso terminó".</summary>
public sealed record CommandExecutionResult(bool Success, int ExitCode, TimeSpan Duration, string StandardOutput, string StandardError);
