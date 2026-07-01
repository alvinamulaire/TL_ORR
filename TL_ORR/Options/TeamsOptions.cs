namespace TL_ORR.Options;

public sealed class TeamsOptions
{
    public string SendMode { get; init; } = "Console";

    public string AuthMode { get; init; } = "DeviceCode";

    public string TenantId { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string TokenCacheName { get; init; } = "TL-ORR-Teams-Delegated";

    public string[] DelegatedScopes { get; init; } = ["ChatMessage.Send", "Chat.ReadWrite", "Chat.Create", "User.Read", "User.ReadBasic.All", "offline_access"];

    public string SenderUserEmail { get; init; } = string.Empty;

    public string TargetUserEmail { get; init; } = string.Empty;

    public string MailApiUrl { get; init; } = string.Empty;

    public string MailApiKey { get; init; } = string.Empty;

    public string CcTo { get; init; } = string.Empty;

    public int HttpTimeoutSeconds { get; init; } = 120;

    public bool InlineImageEnabled { get; init; } = true;

    public int MaxInlineImageBytes { get; init; } = 4 * 1024 * 1024;
}
