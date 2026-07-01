namespace TL_ORR.Services;

public interface INotificationRecipientService
{
    Task<IReadOnlyList<string>> GetRecipientEmailsAsync(CancellationToken cancellationToken);
}
