using MRS.DismEngine.Processes;
using MRS.PostInstall.Models;

namespace MRS.PostInstall.Execution;

/// <summary>
/// Implementación de <see cref="ICommandExecutor"/> sobre <see cref="IProcessRunner"/>
/// (la misma infraestructura de ejecución de procesos que ya usan
/// <c>DismRunner</c>/<c>OfflineRegistryEditor</c>): no duplica la lógica de
/// captura de stdout/stderr, timeout ni cancelación.
/// </summary>
public sealed class CommandExecutor : ICommandExecutor
{
    private readonly IProcessRunner _processRunner;

    public CommandExecutor(IProcessRunner processRunner)
        => _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<CommandExecutionResult> ExecuteAsync(CommandExecutionSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var result = await _processRunner
            .RunAsync(spec.Executable, spec.Arguments, cancellationToken, spec.Timeout, spec.WorkingDirectory)
            .ConfigureAwait(false);

        var success = !result.TimedOut && spec.ExpectedExitCodes.Contains(result.ExitCode);
        return new CommandExecutionResult(success, result.ExitCode, result.Duration, result.StandardOutput, result.StandardError);
    }
}
