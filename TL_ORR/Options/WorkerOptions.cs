namespace TL_ORR.Options;

public sealed class WorkerOptions
{
    public int IntervalSeconds { get; init; } = 60;

    public int BatchSize { get; init; } = 100;

    public bool RunOnce { get; init; }

    public string TestSfcFilter { get; init; } = string.Empty;

    public int StopAfterConsecutiveCycleFailures { get; init; }

    public int SqlCommandTimeoutSeconds { get; init; } = 30;

    public int PerRecordTimeoutSeconds { get; init; } = 120;
}
