using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TL_ORR.Options;

namespace TL_ORR.Services;

public sealed class NotificationRecipientService : INotificationRecipientService
{
    private readonly NotificationRecipientOptions _options;
    private readonly TeamsOptions _teamsOptions;
    private readonly ILogger<NotificationRecipientService> _logger;

    public NotificationRecipientService(
        IOptions<NotificationRecipientOptions> options,
        IOptions<TeamsOptions> teamsOptions,
        ILogger<NotificationRecipientService> logger)
    {
        _options = options.Value;
        _teamsOptions = teamsOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GetRecipientEmailsAsync(CancellationToken cancellationToken)
    {
        if (IsSqlServerSource)
        {
            var recipients = await GetSqlServerRecipientsAsync(cancellationToken);
            if (recipients.Count > 0)
            {
                return recipients;
            }

            _logger.LogWarning("No recipients found from AlertDB. Falling back to Teams:TargetUserEmail.");
        }

        return SplitRecipients(_teamsOptions.TargetUserEmail);
    }

    private async Task<IReadOnlyList<string>> GetSqlServerRecipientsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT LTRIM(RTRIM(CAST(Email AS nvarchar(320)))) AS Email
            FROM dbo.N8N_NotifyLevel_ATT
            WHERE Project_Group = @ProjectGroup
              AND NULLIF(LTRIM(RTRIM(CAST(Email AS nvarchar(320)))), '') IS NOT NULL
            ORDER BY Email;
            """;

        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = Math.Max(1, _options.CommandTimeoutSeconds)
        };
        command.Parameters.AddWithValue("@ProjectGroup", _options.ProjectGroup);

        var recipients = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var email = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(email))
            {
                recipients.Add(email);
            }
        }

        _logger.LogInformation("Loaded {Count} notification recipient(s) from AlertDB. ProjectGroup={ProjectGroup}", recipients.Count, _options.ProjectGroup);
        return recipients;
    }

    private string GetConnectionString()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("NOTIFICATION_RECIPIENTS_CONNECTION_STRING") ??
            _options.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Missing notification recipient connection string. Set NOTIFICATION_RECIPIENTS_CONNECTION_STRING or NotificationRecipients:ConnectionString.");
        }

        return connectionString;
    }

    private bool IsSqlServerSource
    {
        get
        {
            return string.Equals(_options.Source, "SqlServer", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<string> SplitRecipients(string recipients)
    {
        return recipients
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static recipient => !string.IsNullOrWhiteSpace(recipient))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
