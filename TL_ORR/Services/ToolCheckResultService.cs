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
                EMPLOYEE_NO,
                SFC,
                TOOL_ID,
                TOOL_SN,
                CheckResult,
                ImagePath,
                DateTime
            FROM dbo.ToolCheckResult
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

    public async Task MarkTeamsSentAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.ToolCheckResult
            SET
                IsSentTeams = 1,
                SentTeamsTime = GETDATE(),
                SendErrorMessage = NULL
            WHERE ID = @ID;
            """;

        await ExecuteStatusCommandAsync(sql, id, null, cancellationToken);
    }

    public async Task MarkTeamsFailedAsync(int id, string errorMessage, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.ToolCheckResult
            SET
                SendErrorMessage = @ErrorMessage
            WHERE ID = @ID;
            """;

        await ExecuteStatusCommandAsync(sql, id, Truncate(errorMessage, 1000), cancellationToken);
    }

    private async Task ExecuteStatusCommandAsync(string sql, int id, string? errorMessage, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ID", id);

        if (errorMessage is not null)
        {
            command.Parameters.AddWithValue("@ErrorMessage", errorMessage);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
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
