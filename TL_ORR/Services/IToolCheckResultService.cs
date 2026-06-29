using TL_ORR.Models;

namespace TL_ORR.Services;

public interface IToolCheckResultService
{
    Task<IReadOnlyList<ToolCheckResult>> GetPendingNgResultsAsync(int batchSize, CancellationToken cancellationToken);

    Task MarkTeamsSentAsync(int id, CancellationToken cancellationToken);

    Task MarkTeamsFailedAsync(int id, string errorMessage, CancellationToken cancellationToken);
}
