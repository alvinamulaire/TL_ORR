using TL_ORR.Models;

namespace TL_ORR.Services;

public interface INotificationMessageFormatter
{
    string Format(ToolCheckResult result, string imagePath);
}
