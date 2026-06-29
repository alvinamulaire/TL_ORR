using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
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
    }

    public async Task SendAsync(ToolCheckResult result, string imagePath, CancellationToken cancellationToken)
    {
        var content = _messageFormatter.Format(result, imagePath);

        if (!IsGraphMode)
        {
            _logger.LogInformation(
                "Phase 1 Teams message simulation. TargetUserEmail={TargetUserEmail}, ID={Id}, Message={Message}",
                _options.TargetUserEmail,
                result.Id,
                content.Replace("<br>", Environment.NewLine, StringComparison.Ordinal));
            return;
        }

        ValidateOptions();

        var token = await GetAccessTokenAsync(cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var userId = await GetUserIdAsync(_options.TargetUserEmail, cancellationToken);
        var chatId = await CreateOneOnOneChatAsync(userId, cancellationToken);

        await SendChatMessageAsync(chatId, content, cancellationToken);
    }

    private bool IsGraphMode
    {
        get
        {
            return string.Equals(_options.SendMode, "Graph", StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token");

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to get Graph token. Status={(int)response.StatusCode}. Body={responseBody}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody, JsonOptions);
        if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
        {
            throw new InvalidOperationException("Graph token response did not contain access_token.");
        }

        return tokenResponse.AccessToken;
    }

    private async Task<string> GetUserIdAsync(string email, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(email)}?$select=id", cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to find Teams target user. Status={(int)response.StatusCode}. Body={responseBody}");
        }

        var user = JsonSerializer.Deserialize<GraphUserResponse>(responseBody, JsonOptions);
        if (string.IsNullOrWhiteSpace(user?.Id))
        {
            throw new InvalidOperationException($"Graph user response did not contain id for {email}.");
        }

        return user.Id;
    }

    private async Task<string> CreateOneOnOneChatAsync(string userId, CancellationToken cancellationToken)
    {
        var request = new
        {
            chatType = "oneOnOne",
            members = new object[]
            {
                AadUserConversationMember(userId)
            }
        };

        using var response = await _httpClient.PostAsJsonAsync("https://graph.microsoft.com/v1.0/chats", request, JsonOptions, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to create Teams chat. Status={(int)response.StatusCode}. Body={responseBody}");
        }

        var chat = JsonSerializer.Deserialize<GraphChatResponse>(responseBody, JsonOptions);
        if (string.IsNullOrWhiteSpace(chat?.Id))
        {
            throw new InvalidOperationException("Graph chat response did not contain id.");
        }

        return chat.Id;
    }

    private async Task SendChatMessageAsync(string chatId, string content, CancellationToken cancellationToken)
    {
        var request = new
        {
            body = new
            {
                contentType = "html",
                content
            }
        };

        using var response = await _httpClient.PostAsJsonAsync($"https://graph.microsoft.com/v1.0/chats/{chatId}/messages", request, JsonOptions, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to send Teams message. Status={(int)response.StatusCode}. Body={responseBody}");
        }
    }

    private static object AadUserConversationMember(string userId)
    {
        return new Dictionary<string, object>
        {
            ["@odata.type"] = "#microsoft.graph.aadUserConversationMember",
            ["roles"] = new[] { "owner" },
            ["user@odata.bind"] = $"https://graph.microsoft.com/v1.0/users('{userId}')"
        };
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantId) ||
            string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret) ||
            string.IsNullOrWhiteSpace(_options.TargetUserEmail))
        {
            throw new InvalidOperationException("Teams options are incomplete. Configure TenantId, ClientId, ClientSecret, and TargetUserEmail.");
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }

    private sealed class GraphUserResponse
    {
        public string? Id { get; init; }
    }

    private sealed class GraphChatResponse
    {
        public string? Id { get; init; }
    }
}
