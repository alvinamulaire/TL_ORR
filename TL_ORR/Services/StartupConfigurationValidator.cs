using Microsoft.Extensions.Options;
using TL_ORR.Options;

namespace TL_ORR.Services;

public sealed class StartupConfigurationValidator : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly TeamsOptions _teamsOptions;
    private readonly WorkerOptions _workerOptions;
    private readonly FileShareOptions _fileShareOptions;
    private readonly ILogger<StartupConfigurationValidator> _logger;

    public StartupConfigurationValidator(
        IConfiguration configuration,
        IOptions<TeamsOptions> teamsOptions,
        IOptions<WorkerOptions> workerOptions,
        IOptions<FileShareOptions> fileShareOptions,
        ILogger<StartupConfigurationValidator> logger)
    {
        _configuration = configuration;
        _teamsOptions = teamsOptions.Value;
        _workerOptions = workerOptions.Value;
        _fileShareOptions = fileShareOptions.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Configuration validation failed: " + string.Join("; ", errors));
        }

        _logger.LogInformation("Configuration validation passed.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private List<string> Validate()
    {
        var errors = new List<string>();
        var connectionString = GetConnectionString();

        if (IsMissingOrPlaceholder(connectionString, "YOUR_"))
        {
            errors.Add("ConnectionStrings:DefaultConnection must be configured.");
        }

        if (_workerOptions.IntervalSeconds <= 0)
        {
            errors.Add("Worker:IntervalSeconds must be greater than 0.");
        }

        if (_workerOptions.BatchSize <= 0)
        {
            errors.Add("Worker:BatchSize must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(_fileShareOptions.ServerIP))
        {
            errors.Add("FileShare:ServerIP must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_fileShareOptions.ShareName))
        {
            errors.Add("FileShare:ShareName must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_teamsOptions.TargetUserEmail))
        {
            errors.Add("Teams:TargetUserEmail must be configured.");
        }

        if (!IsSupportedSendMode(_teamsOptions.SendMode))
        {
            errors.Add("Teams:SendMode must be Console or Graph.");
        }

        if (IsGraphMode)
        {
            ValidateGraphOptions(errors);
        }

        if (IsAmulaireMailApiMode)
        {
            ValidateAmulaireMailApiOptions(errors);
        }

        return errors;
    }

    private void ValidateGraphOptions(List<string> errors)
    {
        if (IsMissingOrPlaceholder(_teamsOptions.TenantId, "YOUR_"))
        {
            errors.Add("Teams:TenantId must be configured when Teams:SendMode is Graph.");
        }

        if (IsMissingOrPlaceholder(_teamsOptions.ClientId, "YOUR_"))
        {
            errors.Add("Teams:ClientId must be configured when Teams:SendMode is Graph.");
        }

        if (IsMissingOrPlaceholder(_teamsOptions.ClientSecret, "YOUR_"))
        {
            errors.Add("Teams:ClientSecret must be configured when Teams:SendMode is Graph.");
        }

        if (!string.Equals(_teamsOptions.AuthMode, "DelegatedRefreshToken", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Teams:AuthMode must be DelegatedRefreshToken when Teams:SendMode is Graph.");
        }

        if (IsMissingOrPlaceholder(_teamsOptions.RefreshToken, "YOUR_"))
        {
            errors.Add("Teams:RefreshToken must be configured when Teams:SendMode is Graph.");
        }

        if (string.IsNullOrWhiteSpace(_teamsOptions.SenderUserEmail))
        {
            errors.Add("Teams:SenderUserEmail must be configured when Teams:SendMode is Graph.");
        }
    }

    private void ValidateAmulaireMailApiOptions(List<string> errors)
    {
        if (IsMissingOrPlaceholder(_teamsOptions.MailApiUrl, "YOUR_"))
        {
            errors.Add("Teams:MailApiUrl must be configured when Teams:SendMode is AmulaireMailApi.");
        }

        if (IsMissingOrPlaceholder(_teamsOptions.MailApiKey, "YOUR_"))
        {
            errors.Add("Teams:MailApiKey must be configured when Teams:SendMode is AmulaireMailApi.");
        }
    }

    private bool IsGraphMode
    {
        get
        {
            return string.Equals(_teamsOptions.SendMode, "Graph", StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool IsAmulaireMailApiMode
    {
        get
        {
            return string.Equals(_teamsOptions.SendMode, "AmulaireMailApi", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool IsSupportedSendMode(string? sendMode)
    {
        return string.Equals(sendMode, "Console", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sendMode, "Graph", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sendMode, "AmulaireMailApi", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMissingOrPlaceholder(string? value, string placeholderPrefix)
    {
        return string.IsNullOrWhiteSpace(value) ||
               value.Contains(placeholderPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private string? GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING") ??
               _configuration.GetConnectionString("DefaultConnection");
    }
}
