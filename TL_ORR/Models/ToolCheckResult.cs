namespace TL_ORR.Models;

public sealed class ToolCheckResult
{
    public string RecordKey
    {
        get
        {
            return $"{EmployeeNo}/{Sfc}/{ToolId}/{ToolSn}/{CheckedAt:O}/{ImagePath}";
        }
    }

    public string EmployeeNo { get; init; } = string.Empty;

    public string Sfc { get; init; } = string.Empty;

    public string ToolId { get; init; } = string.Empty;

    public string ToolSn { get; init; } = string.Empty;

    public string CheckResult { get; init; } = string.Empty;

    public string? ImagePath { get; init; }

    public DateTime CheckedAt { get; init; }
}
