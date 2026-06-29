namespace TL_ORR.Options;

public sealed class WorkerOptions
{
    public int IntervalSeconds { get; init; } = 60;

    public int BatchSize { get; init; } = 100;

    public bool RunOnce { get; init; }
}
