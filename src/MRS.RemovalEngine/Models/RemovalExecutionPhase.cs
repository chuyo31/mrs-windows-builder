namespace MRS.RemovalEngine.Models;

public enum RemovalExecutionPhase
{
    NotStarted,
    PreFlight,
    Mounting,
    Executing,
    Committing,
    Discarding,
    Completed,
    Failed,
    Cancelled,
}
