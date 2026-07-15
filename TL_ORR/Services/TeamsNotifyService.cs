using System.Net.Http.Json;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using TL_ORR.Models;
using TL_ORR.Options;

namespace TL_ORR.Services;

public sealed class TeamsNotifyService : ITeamsNotifyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TeamsOptions _options;
    private readonly INotificationRecipientService _recipientService;
    private readonly INotificationMessageFormatter _messageFormatter;
    private readonly ILogger<TeamsNotifyService> _logger;
    private readonly Lazy<GraphServiceClient> _graphClient;

    public TeamsNotifyService(
        HttpClient httpClient,
        IOptions<TeamsOptions> options,
        INotificationRecipientService recipientService,
        INotificationMessageFormatter messageFormatter,
        ILogger<TeamsNotifyService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _recipientService = recipientService;
        _messageFormatter = messageFormatter;
        _logger = logger;
        _graphClient = new Lazy<GraphServiceClient>(CreateGraphClient);
    }

    public async Task SendAsync(ToolCheckResult result, string imagePath, CancellationToken cancellationToken)
    {
        var content = _messageFormatter.Format(result, imagePath);
        var recipientEmails = await _recipientService.GetRecipientEmailsAsync(cancellationToken);
        if (recipientEmails.Count == 0)
        {
            throw new InvalidOperationException("No Teams notification recipients were resolved.");
        }

        if (IsConsoleMode)
        {
            _logger.LogInformation(
                "Phase 1 Teams message simulation. TargetUserEmails={TargetUserEmails}, RecordKey={RecordKey}, Message={Message}",
                string.Join(';', recipientEmails),
                result.RecordKey,
                content.Replace("<br>", Environment.NewLine, StringComparison.Ordinal));
            return;
        }

        if (IsAmulaireMailApiMode)
        {
            await SendAmulaireMailAsync(result, content, recipientEmails, cancellationToken);
            return;
        }

        ValidateOptions();

        await SendGraphTeamsMessagesAsync(content, imagePath, recipientEmails, cancellationToken);
    }

    private bool IsConsoleMode
    {
        get
        {
            return string.Equals(_options.SendMode, "Console", StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool IsAmulaireMailApiMode
    {
        get
        {
            return string.Equals(_options.SendMode, "AmulaireMailApi", StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task SendAmulaireMailAsync(ToolCheckResult result, string content, IReadOnlyList<string> recipientEmails, CancellationToken cancellationToken)
    {
        ValidateAmulaireMailApiOptions();

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.MailApiUrl);
        request.Headers.Add("X-Api-Key", _options.MailApiKey);

        var mailRequest = new SendMailRequest
        {
            MailTo = recipientEmails,
            CcTo = SplitRecipients(_options.CcTo),
            MailSubject = $"Tool NG Check Notification - SFC {result.Sfc}",
            MailBody = content,
            IsBodyHtmlFormat = true,
            UseTemplate = true
        };

        request.Content = JsonContent.Create(mailRequest, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to send notification through Amulaire Mail API. Status={(int)response.StatusCode}. Body={responseBody}");
        }
    }

    private async Task SendGraphTeamsMessagesAsync(string content, string imagePath, IReadOnlyList<string> recipientEmails, CancellationToken cancellationToken)
    {
        var senderUserEmail = NormalizeGraphUserAddress(_options.SenderUserEmail);
        var normalizedRecipientEmails = recipientEmails
            .Select(NormalizeGraphUserAddress)
            .Where(static recipient => !string.IsNullOrWhiteSpace(recipient))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _logger.LogInformation(
            "Preparing Teams direct message(s). SenderUserEmail={SenderUserEmail}, TargetUserEmails={TargetUserEmails}",
            senderUserEmail,
            string.Join(';', normalizedRecipientEmails));

        var senderUserId = await ResolveUserIdAsync(senderUserEmail, cancellationToken);
        if (string.IsNullOrWhiteSpace(senderUserId))
        {
            throw new InvalidOperationException($"Cannot resolve sender Teams user. Sender={senderUserEmail}");
        }

        foreach (var recipientEmail in normalizedRecipientEmails)
        {
            await SendGraphTeamsMessageAsync(content, imagePath, senderUserId, recipientEmail, cancellationToken);
        }
    }

    private async Task SendGraphTeamsMessageAsync(string content, string imagePath, string senderUserId, string recipientEmail, CancellationToken cancellationToken)
    {
        var targetUserId = await ResolveUserIdAsync(recipientEmail, cancellationToken);
        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            throw new InvalidOperationException($"Cannot resolve target Teams user. Target={recipientEmail}");
        }

        _logger.LogInformation("Resolved Teams users. SenderUserEmail={SenderUserEmail}, TargetUserEmail={TargetUserEmail}", _options.SenderUserEmail, recipientEmail);

        var chat = await _graphClient.Value.Chats.PostAsync(
            new Chat
            {
                ChatType = ChatType.OneOnOne,
                Members =
                [
                    BuildChatMember(senderUserId),
                    BuildChatMember(targetUserId)
                ]
            },
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(chat?.Id))
        {
            throw new InvalidOperationException($"Cannot create or resolve one-on-one chat for {recipientEmail}.");
        }

        _logger.LogInformation("Teams one-on-one chat is ready. TargetUserEmail={TargetUserEmail}, ChatId={ChatId}", recipientEmail, chat.Id);

        var message = await BuildGraphMessageAsync(content, imagePath, cancellationToken);

        await _graphClient.Value.Chats[chat.Id].Messages.PostAsync(message, cancellationToken: cancellationToken);

        _logger.LogInformation("Teams direct message sent to {TargetUserEmail}.", recipientEmail);
    }

    private async Task<ChatMessage> BuildGraphMessageAsync(string content, string imagePath, CancellationToken cancellationToken)
    {
        var message = new ChatMessage
        {
            Body = new ItemBody
            {
                ContentType = BodyType.Html,
                Content = content
            }
        };

        var inlineImage = await TryBuildInlineImageAsync(imagePath, cancellationToken);
        if (inlineImage is null)
        {
            return message;
        }

        message.Body.Content = $"{content}<br><img src=\"../hostedContents/1/$value\" alt=\"Tool NG image\" />";
        message.HostedContents = [inlineImage];

        return message;
    }

    private async Task<ChatMessageHostedContent?> TryBuildInlineImageAsync(string imagePath, CancellationToken cancellationToken)
    {
        if (!_options.InlineImageEnabled || string.IsNullOrWhiteSpace(imagePath))
        {
            return null;
        }

        if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            _logger.LogInformation("Skipping Graph hosted content for HTTP image path. ImagePath={ImagePath}", imagePath);
            return null;
        }

        try
        {
            var fileInfo = new FileInfo(imagePath);
            if (!fileInfo.Exists)
            {
                _logger.LogWarning("Inline image file does not exist or is not accessible. ImagePath={ImagePath}", imagePath);
                return null;
            }

            if (fileInfo.Length > _options.MaxInlineImageBytes)
            {
                _logger.LogWarning(
                    "Inline image file is too large. ImagePath={ImagePath}, Size={Size}, MaxInlineImageBytes={MaxInlineImageBytes}",
                    imagePath,
                    fileInfo.Length,
                    _options.MaxInlineImageBytes);
                return null;
            }

            var contentBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
            var contentType = GetImageContentType(fileInfo.Extension);
            _logger.LogInformation("Inline image loaded for Teams message. ImagePath={ImagePath}, Size={Size}, ContentType={ContentType}", imagePath, contentBytes.Length, contentType);

            return new ChatMessageHostedContent
            {
                ContentBytes = contentBytes,
                ContentType = contentType,
                AdditionalData = new Dictionary<string, object>
                {
                    ["@microsoft.graph.temporaryId"] = "1"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to load inline image. The Teams message will include text path only. ImagePath={ImagePath}", imagePath);
            return null;
        }
    }

    private async Task<string?> ResolveUserIdAsync(string userAddressOrId, CancellationToken cancellationToken)
    {
        var normalizedUserAddressOrId = NormalizeGraphUserAddress(userAddressOrId);
        if (string.IsNullOrWhiteSpace(normalizedUserAddressOrId))
        {
            return null;
        }

        try
        {
            var user = await _graphClient.Value.Users[normalizedUserAddressOrId].GetAsync(cancellationToken: cancellationToken);
            return user?.Id;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Cannot resolve Teams user through Microsoft Graph. User={normalizedUserAddressOrId}. " +
                "If a device-code sign-in prompt was just printed, complete that sign-in with the service account and run again. " +
                $"GraphError={ex.Message}",
                ex);
        }
    }

    private GraphServiceClient CreateGraphClient()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantId) || string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("Teams Graph device code mode requires TenantId and ClientId.");
        }

        var delegatedCredential = new DeviceCodeCredential(new DeviceCodeCredentialOptions
        {
            TenantId = _options.TenantId,
            ClientId = _options.ClientId,
            DeviceCodeCallback = async (code, _) =>
            {
                _logger.LogWarning("Teams delegated sign-in required. {Message}", code.Message);
                await Task.CompletedTask;
            },
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions
            {
                Name = string.IsNullOrWhiteSpace(_options.TokenCacheName)
                    ? "TL-ORR-Teams-Delegated"
                    : _options.TokenCacheName
            }
        });

        var delegatedScopes = _options.DelegatedScopes.Length == 0
            ? ["Chat.ReadWrite", "ChatMessage.Send", "User.Read"]
            : _options.DelegatedScopes;

        return new GraphServiceClient(delegatedCredential, delegatedScopes);
    }

    private static AadUserConversationMember BuildChatMember(string userId)
    {
        return new AadUserConversationMember
        {
            Roles = ["owner"],
            AdditionalData = new Dictionary<string, object>
            {
                ["user@odata.bind"] = $"https://graph.microsoft.com/v1.0/users('{userId}')"
            }
        };
    }

    private static string GetImageContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantId) ||
            string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.SenderUserEmail))
        {
            throw new InvalidOperationException("Teams options are incomplete. Configure TenantId, ClientId, and SenderUserEmail.");
        }
    }

    private void ValidateAmulaireMailApiOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.MailApiUrl) ||
            string.IsNullOrWhiteSpace(_options.MailApiKey))
        {
            throw new InvalidOperationException("Amulaire Mail API options are incomplete. Configure MailApiUrl and MailApiKey.");
        }
    }

    private static IReadOnlyList<string> SplitRecipients(string recipients)
    {
        return recipients
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeGraphUserAddress)
            .Where(static recipient => !string.IsNullOrWhiteSpace(recipient))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeGraphUserAddress(string userAddressOrId)
    {
        return userAddressOrId
            .Trim()
            .Trim('\uFEFF', '\u200B', '\u200C', '\u200D');
    }

}
