using TL_ORR.Models;

namespace TL_ORR.Services;

public interface IToolCheckResultService
{
    Task<IReadOnlyList<ToolCheckResult>> GetPendingNgResultsAsync(int batchSize, CancellationToken cancellationToken);

    Task MarkTeamsSentAsync(ToolCheckResult result, CancellationToken cancellationToken);

    Task MarkTeamsFailedAsync(ToolCheckResult result, string errorMessage, CancellationToken cancellationToken);
}
