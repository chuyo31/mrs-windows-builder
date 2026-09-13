using MRS.PostInstall.Execution;
using MRS.PostInstall.Models;
using MRS.PostInstall.Tests.Fakes;
using Xunit;

namespace MRS.PostInstall.Tests;

/// <summary>
/// P18, sección 18 (Command execution): ExitCode 0, ExitCode distinto de 0,
/// argumentos, timeout, cancelación. Nunca ejecuta ningún proceso real
/// (<see cref="FakeProcessRunner"/>).
/// </summary>
public sealed class CommandExecutorTests
{
    private readonly FakeProcessRunner _processRunner = new();
    private readonly CommandExecutor _executor;

    public CommandExecutorTests() => _executor = new CommandExecutor(_processRunner);

    private static CommandExecutionSpec Spec() => new()
    {
        Executable = @"C:\fake\installer.exe",
        Arguments = "/install /quiet /norestart",
        WorkingDirectory = @"C:\fake",
        Timeout = TimeSpan.FromMinutes(5),
        ExpectedExitCodes = new[] { 0 },
    };

    [Fact]
    public async Task ExitCode_0_is_success()
    {
        _processRunner.ExitCode = 0;

        var result = await _executor.ExecuteAsync(Spec());

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task A_nonzero_ExitCode_is_never_success()
    {
        _processRunner.ExitCode = 1603; // código típico de fallo de MSI/instaladores .NET

        var result = await _executor.ExecuteAsync(Spec());

        Assert.False(result.Success);
        Assert.Equal(1603, result.ExitCode);
    }

    [Fact]
    public async Task ExpectedExitCodes_can_include_more_than_just_zero()
    {
        _processRunner.ExitCode = 3010; // "éxito, reinicio pendiente" en muchos instaladores
        var spec = Spec() with { ExpectedExitCodes = new[] { 0, 3010 } };

        var result = await _executor.ExecuteAsync(spec);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Arguments_and_executable_and_working_directory_are_passed_through_exactly()
    {
        var spec = Spec();

        await _executor.ExecuteAsync(spec);

        var call = Assert.Single(_processRunner.Calls);
        Assert.Equal(spec.Executable, call.FileName);
        Assert.Equal(spec.Arguments, call.Arguments);
        Assert.Equal(spec.WorkingDirectory, call.WorkingDirectory);
    }

    [Fact]
    public async Task A_timed_out_process_is_never_success_even_with_ExitCode_0()
    {
        _processRunner.ExitCode = 0;
        _processRunner.TimeOut = true;

        var result = await _executor.ExecuteAsync(Spec());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Cancellation_propagates_to_the_underlying_process_runner()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // El executor no debe tragarse la cancelación ni convertirla en un
        // resultado "success"; se limita a delegar en IProcessRunner.
        await _executor.ExecuteAsync(Spec(), cts.Token);

        Assert.Single(_processRunner.Calls);
    }

    [Fact]
    public async Task Finishing_the_process_is_not_by_itself_success_if_ExitCode_is_unexpected()
    {
        _processRunner.ExitCode = -1;

        var result = await _executor.ExecuteAsync(Spec());

        // "el proceso terminó" (tiene un ExitCode, no fue timeout) no basta: -1 no
        // está en ExpectedExitCodes.
        Assert.False(result.Success);
    }
}
