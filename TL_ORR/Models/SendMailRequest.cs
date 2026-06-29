using System.Text.Json.Serialization;

namespace TL_ORR.Models;

public sealed class SendMailRequest
{
    public IReadOnlyList<string> MailTo { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string>? CcTo { get; init; }

    [JsonPropertyName("MailSubjict")]
    public string MailSubject { get; init; } = string.Empty;

    public string MailBody { get; init; } = string.Empty;

    public bool IsBodyHtmlFormat { get; init; }

    public bool UseTemplate { get; init; }
}
