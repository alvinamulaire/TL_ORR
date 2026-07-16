using System.Data.SqlClient;
using Microsoft.Extensions.Options;
using TL_ORR.Models;
using TL_ORR.Options;

namespace TL_ORR.Services;

public sealed class ToolCheckResultService : IToolCheckResultService
{
    private readonly IConfiguration _configuration;
    private readonly WorkerOptions _workerOptions;
    private readonly ILogger<ToolCheckResultService> _logger;

    public ToolCheckResultService(
        IConfiguration configuration,
        IOptions<WorkerOptions> workerOptions,
        ILogger<ToolCheckResultService> logger)
    {
        _configuration = configuration;
        _workerOptions = workerOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ToolCheckResult>> GetPendingNgResultsAsync(int batchSize, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@BatchSize)
                ID,
                CAST(EMPLOYEE_NO AS nvarchar(100)) AS EMPLOYEE_NO,
                CAST(SFC AS nvarchar(100)) AS SFC,
                CAST(TOOL_ID AS nvarchar(100)) AS TOOL_ID,
                CAST(TOOL_SN AS nvarchar(100)) AS TOOL_SN,
                CAST(CheckResult AS nvarchar(20)) AS CheckResult,
                ImagePath,
                DateTime
            FROM dbo.ProductIns
            WHERE CheckResult = 'NG'
              AND IsSentTeams = 0
              AND (@TestSfcFilter = '' OR CAST(SFC AS nvarchar(100)) = @TestSfcFilter)
            ORDER BY DateTime ASC;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = SqlCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@BatchSize", Math.Max(1, batchSize));
        command.Parameters.AddWithValue("@TestSfcFilter", _workerOptions.TestSfcFilter.Trim());

        var results = new List<ToolCheckResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ToolCheckResult
            {
                Id = reader.GetInt32(0),
                EmployeeNo = GetStringOrEmpty(reader, 1),
                Sfc = GetStringOrEmpty(reader, 2),
                ToolId = GetStringOrEmpty(reader, 3),
                ToolSn = GetStringOrEmpty(reader, 4),
                CheckResult = GetStringOrEmpty(reader, 5),
                ImagePath = reader.IsDBNull(6) ? null : reader.GetString(6),
                CheckedAt = reader.GetDateTime(7)
            });
        }

        return results;
    }

    public async Task MarkTeamsSentAsync(ToolCheckResult result, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.ProductIns
            SET
                IsSentTeams = 1,
                SentTeamsTime = GETDATE(),
                SendErrorMessage = NULL
            WHERE ID = @ID
              AND CheckResult = 'NG'
              AND IsSentTeams = 0;
            """;

        await ExecuteStatusCommandAsync(sql, result, null, cancellationToken);
    }

    public async Task MarkTeamsFailedAsync(ToolCheckResult result, string errorMessage, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.ProductIns
            SET
                SendErrorMessage = @ErrorMessage
            WHERE ID = @ID
              AND CheckResult = 'NG'
              AND IsSentTeams = 0;
            """;

        await ExecuteStatusCommandAsync(sql, result, Truncate(errorMessage, 1000), cancellationToken);
    }

    private async Task ExecuteStatusCommandAsync(string sql, ToolCheckResult result, string? errorMessage, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = SqlCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@ID", result.Id);

        if (errorMessage is not null)
        {
            command.Parameters.AddWithValue("@ErrorMessage", errorMessage);
        }

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows == 0)
        {
            if (await IsAlreadyMarkedSentAsync(connection, result, cancellationToken))
            {
                _logger.LogWarning(
                    "ProductIns row was already marked as sent before status update. Treating as successful. RecordKey={RecordKey}",
                    result.RecordKey);
                return;
            }

            throw new InvalidOperationException($"No pending ProductIns row was updated. RecordKey={result.RecordKey}");
        }
    }

    private async Task<bool> IsAlreadyMarkedSentAsync(SqlConnection connection, ToolCheckResult result, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                CAST(CheckResult AS nvarchar(20)) AS CheckResult,
                IsSentTeams
            FROM dbo.ProductIns
            WHERE ID = @ID;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = SqlCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@ID", result.Id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return false;
        }

        var checkResult = GetStringOrEmpty(reader, 0);
        var isSentTeams = !reader.IsDBNull(1) && reader.GetBoolean(1);
        return string.Equals(checkResult, "NG", StringComparison.OrdinalIgnoreCase) && isSentTeams;
    }

    private SqlConnection CreateConnection()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING") ??
            _configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Missing SQL Server connection string. Set MSSQL_CONNECTION_STRING or ConnectionStrings:DefaultConnection.");
        }

        return new SqlConnection(connectionString);
    }

    private int SqlCommandTimeoutSeconds
    {
        get
        {
            return Math.Max(1, _workerOptions.SqlCommandTimeoutSeconds);
        }
    }

    private static string GetStringOrEmpty(SqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
