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
                EmployeeNo = reader.GetString(0),
                Sfc = reader.GetString(1),
                ToolId = reader.GetString(2),
                ToolSn = reader.GetString(3),
                CheckResult = reader.GetString(4),
                ImagePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                CheckedAt = reader.GetDateTime(6)
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
            WHERE CAST(EMPLOYEE_NO AS nvarchar(100)) = @EMPLOYEE_NO
              AND CAST(SFC AS nvarchar(100)) = @SFC
              AND CAST(TOOL_ID AS nvarchar(100)) = @TOOL_ID
              AND CAST(TOOL_SN AS nvarchar(100)) = @TOOL_SN
              AND DateTime = @DateTime
              AND (
                    (ImagePath IS NULL AND @ImagePath IS NULL)
                    OR ImagePath = @ImagePath
                  )
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
            WHERE CAST(EMPLOYEE_NO AS nvarchar(100)) = @EMPLOYEE_NO
              AND CAST(SFC AS nvarchar(100)) = @SFC
              AND CAST(TOOL_ID AS nvarchar(100)) = @TOOL_ID
              AND CAST(TOOL_SN AS nvarchar(100)) = @TOOL_SN
              AND DateTime = @DateTime
              AND (
                    (ImagePath IS NULL AND @ImagePath IS NULL)
                    OR ImagePath = @ImagePath
                  )
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
        command.Parameters.AddWithValue("@EMPLOYEE_NO", result.EmployeeNo);
        command.Parameters.AddWithValue("@SFC", result.Sfc);
        command.Parameters.AddWithValue("@TOOL_ID", result.ToolId);
        command.Parameters.AddWithValue("@TOOL_SN", result.ToolSn);
        command.Parameters.AddWithValue("@DateTime", result.CheckedAt);
        command.Parameters.AddWithValue("@ImagePath", (object?)result.ImagePath ?? DBNull.Value);

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
