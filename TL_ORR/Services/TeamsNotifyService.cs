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
    private readonly INotificationMessageFormatter _messageFormatter;
    private readonly ILogger<TeamsNotifyService> _logger;
    private readonly Lazy<GraphServiceClient> _graphClient;

    public TeamsNotifyService(
        HttpClient httpClient,
        IOptions<TeamsOptions> options,
        INotificationMessageFormatter messageFormatter,
        ILogger<TeamsNotifyService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _messageFormatter = messageFormatter;
        _logger = logger;
        _graphClient = new Lazy<GraphServiceClient>(CreateGraphClient);
    }

    public async Task SendAsync(ToolCheckResult result, string imagePath, CancellationToken cancellationToken)
    {
        var content = _messageFormatter.Format(result, imagePath);

        if (IsConsoleMode)
        {
            _logger.LogInformation(
                "Phase 1 Teams message simulation. TargetUserEmail={TargetUserEmail}, RecordKey={RecordKey}, Message={Message}",
                _options.TargetUserEmail,
                result.RecordKey,
                content.Replace("<br>", Environment.NewLine, StringComparison.Ordinal));
            return;
        }

        if (IsAmulaireMailApiMode)
        {
            await SendAmulaireMailAsync(result, content, cancellationToken);
            return;
        }

        ValidateOptions();

        await SendGraphTeamsMessageAsync(content, cancellationToken);
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

    private async Task SendAmulaireMailAsync(ToolCheckResult result, string content, CancellationToken cancellationToken)
    {
        ValidateAmulaireMailApiOptions();

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.MailApiUrl);
        request.Headers.Add("X-Api-Key", _options.MailApiKey);

        var mailRequest = new SendMailRequest
        {
            MailTo = SplitRecipients(_options.TargetUserEmail),
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

    private async Task SendGraphTeamsMessageAsync(string content, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Preparing Teams direct message. SenderUserEmail={SenderUserEmail}, TargetUserEmail={TargetUserEmail}",
            _options.SenderUserEmail,
            _options.TargetUserEmail);

        var senderUserId = await ResolveUserIdAsync(_options.SenderUserEmail, cancellationToken);
        var targetUserId = await ResolveUserIdAsync(_options.TargetUserEmail, cancellationToken);
        if (string.IsNullOrWhiteSpace(senderUserId) || string.IsNullOrWhiteSpace(targetUserId))
        {
            throw new InvalidOperationException($"Cannot resolve sender or target Teams user. Sender={_options.SenderUserEmail}, Target={_options.TargetUserEmail}");
        }

        _logger.LogInformation("Resolved Teams users. SenderUserEmail={SenderUserEmail}, TargetUserEmail={TargetUserEmail}", _options.SenderUserEmail, _options.TargetUserEmail);

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
            throw new InvalidOperationException($"Cannot create or resolve one-on-one chat for {_options.TargetUserEmail}.");
        }

        _logger.LogInformation("Teams one-on-one chat is ready. TargetUserEmail={TargetUserEmail}, ChatId={ChatId}", _options.TargetUserEmail, chat.Id);

        await _graphClient.Value.Chats[chat.Id].Messages.PostAsync(
            new ChatMessage
            {
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = content
                }
            },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Teams direct message sent to {TargetUserEmail}.", _options.TargetUserEmail);
    }

    private async Task<string?> ResolveUserIdAsync(string userAddressOrId, CancellationToken cancellationToken)
    {
        var user = await _graphClient.Value.Users[userAddressOrId].GetAsync(cancellationToken: cancellationToken);
        return user?.Id;
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

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantId) ||
            string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.SenderUserEmail) ||
            string.IsNullOrWhiteSpace(_options.TargetUserEmail))
        {
            throw new InvalidOperationException("Teams options are incomplete. Configure TenantId, ClientId, SenderUserEmail, and TargetUserEmail.");
        }
    }

    private void ValidateAmulaireMailApiOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.MailApiUrl) ||
            string.IsNullOrWhiteSpace(_options.MailApiKey) ||
            string.IsNullOrWhiteSpace(_options.TargetUserEmail))
        {
            throw new InvalidOperationException("Amulaire Mail API options are incomplete. Configure MailApiUrl, MailApiKey, and TargetUserEmail.");
        }
    }

    private static IReadOnlyList<string> SplitRecipients(string recipients)
    {
        return recipients
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static recipient => !string.IsNullOrWhiteSpace(recipient))
            .ToArray();
    }

}
