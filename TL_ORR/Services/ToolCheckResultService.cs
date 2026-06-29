using Microsoft.Data.SqlClient;
using TL_ORR.Models;

namespace TL_ORR.Services;

public sealed class ToolCheckResultService : IToolCheckResultService
{
    private readonly IConfiguration _configuration;

    public ToolCheckResultService(IConfiguration configuration)
    {
        _configuration = configuration;
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
            ORDER BY DateTime ASC;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BatchSize", Math.Max(1, batchSize));

        var results = new List<ToolCheckResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ToolCheckResult
            {
                Id = reader.GetInt32(0),
                EmployeeNo = reader.GetString(1),
                Sfc = reader.GetString(2),
                ToolId = reader.GetString(3),
                ToolSn = reader.GetString(4),
                CheckResult = reader.GetString(5),
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
        command.Parameters.AddWithValue("@ID", result.Id);

        if (errorMessage is not null)
        {
            command.Parameters.AddWithValue("@ErrorMessage", errorMessage);
        }

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"No pending ProductIns row was updated. RecordKey={result.RecordKey}");
        }
    }

    private SqlConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        }

        return new SqlConnection(connectionString);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
