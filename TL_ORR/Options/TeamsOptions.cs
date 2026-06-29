namespace TL_ORR.Options;

public sealed class TeamsOptions
{
    public string SendMode { get; init; } = "Console";

    public string AuthMode { get; init; } = "DelegatedRefreshToken";

    public string TenantId { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public string SenderUserEmail { get; init; } = string.Empty;

    public string TargetUserEmail { get; init; } = string.Empty;
}
