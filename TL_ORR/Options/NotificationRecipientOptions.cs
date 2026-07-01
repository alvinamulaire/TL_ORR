namespace TL_ORR.Options;

public sealed class NotificationRecipientOptions
{
    public string Source { get; init; } = "SqlServer";

    public string ConnectionString { get; init; } = string.Empty;

    public int ProjectGroup { get; init; } = 1;

    public int CommandTimeoutSeconds { get; init; } = 30;
}
