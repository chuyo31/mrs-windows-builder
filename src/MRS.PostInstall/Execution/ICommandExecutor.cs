using MRS.PostInstall.Models;

namespace MRS.PostInstall.Execution;

/// <summary>Ejecuta un <see cref="CommandExecutionSpec"/> y decide éxito solo por <c>ExitCode</c> (nunca porque "el proceso terminó").</summary>
public interface ICommandExecutor
{
    Task<CommandExecutionResult> ExecuteAsync(CommandExecutionSpec spec, CancellationToken cancellationToken = default);
}
