using TL_ORR.Models;

namespace TL_ORR.Services;

public interface ITeamsNotifyService
{
    Task SendAsync(ToolCheckResult result, string imagePath, CancellationToken cancellationToken);
}
