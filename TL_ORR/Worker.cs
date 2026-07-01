namespace TL_ORR
{
    using Microsoft.Extensions.Options;
    using TL_ORR.Options;
    using TL_ORR.Services;

    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IToolCheckResultService _toolCheckResultService;
        private readonly ITeamsNotifyService _teamsNotifyService;
        private readonly IUncPathConverter _uncPathConverter;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly WorkerOptions _options;
        private readonly TeamsOptions _teamsOptions;
        private readonly FileShareOptions _fileShareOptions;
        private readonly NotificationRecipientOptions _recipientOptions;
        private int _consecutiveCycleFailures;

        public Worker(
            ILogger<Worker> logger,
            IToolCheckResultService toolCheckResultService,
            ITeamsNotifyService teamsNotifyService,
            IUncPathConverter uncPathConverter,
            IHostApplicationLifetime hostApplicationLifetime,
            IOptions<WorkerOptions> options,
            IOptions<TeamsOptions> teamsOptions,
            IOptions<FileShareOptions> fileShareOptions,
            IOptions<NotificationRecipientOptions> recipientOptions)
        {
            _logger = logger;
            _toolCheckResultService = toolCheckResultService;
            _teamsNotifyService = teamsNotifyService;
            _uncPathConverter = uncPathConverter;
            _hostApplicationLifetime = hostApplicationLifetime;
            _options = options.Value;
            _teamsOptions = teamsOptions.Value;
            _fileShareOptions = fileShareOptions.Value;
            _recipientOptions = recipientOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Teams NG notification worker started. SendMode={SendMode}, RecipientSource={RecipientSource}, RecipientProjectGroup={RecipientProjectGroup}, IntervalSeconds={IntervalSeconds}, BatchSize={BatchSize}, RunOnce={RunOnce}, PerRecordTimeoutSeconds={PerRecordTimeoutSeconds}, StopAfterConsecutiveCycleFailures={StopAfterConsecutiveCycleFailures}, FileShare=\\\\{ServerIP}\\{ShareName}",
                _teamsOptions.SendMode,
                _recipientOptions.Source,
                _recipientOptions.ProjectGroup,
                IntervalSeconds,
                BatchSize,
                _options.RunOnce,
                PerRecordTimeoutSeconds,
                _options.StopAfterConsecutiveCycleFailures,
                _fileShareOptions.ServerIP,
                _fileShareOptions.ShareName);

            if (!string.IsNullOrWhiteSpace(_options.TestSfcFilter))
            {
                _logger.LogWarning("Worker:TestSfcFilter is active. Only SFC={Sfc} will be processed.", _options.TestSfcFilter);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingResultsAsync(stoppingToken);
                    _consecutiveCycleFailures = 0;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _consecutiveCycleFailures++;
                    _logger.LogError(
                        ex,
                        "Worker cycle failed. ConsecutiveCycleFailures={ConsecutiveCycleFailures}, StopAfterConsecutiveCycleFailures={StopAfterConsecutiveCycleFailures}. The next cycle will retry unless the stop threshold is reached.",
                        _consecutiveCycleFailures,
                        _options.StopAfterConsecutiveCycleFailures);

                    if (ShouldStopAfterConsecutiveFailures)
                    {
                        _logger.LogCritical(
                            "Worker reached consecutive failure threshold. ConsecutiveCycleFailures={ConsecutiveCycleFailures}. Stopping host.",
                            _consecutiveCycleFailures);
                        _hostApplicationLifetime.StopApplication();
                        break;
                    }
                }

                if (_options.RunOnce)
                {
                    _logger.LogInformation("Worker:RunOnce is true. Stopping host after one cycle.");
                    _hostApplicationLifetime.StopApplication();
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("Teams NG notification worker stopped.");
        }

        private async Task ProcessPendingResultsAsync(CancellationToken cancellationToken)
        {
            var pendingResults = await _toolCheckResultService.GetPendingNgResultsAsync(BatchSize, cancellationToken);
            if (pendingResults.Count == 0)
            {
                _logger.LogInformation("No pending NG tool check results found.");
                return;
            }

            _logger.LogInformation("Found {Count} pending NG tool check result(s).", pendingResults.Count);

            foreach (var result in pendingResults)
            {
                using var recordTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                recordTimeout.CancelAfter(TimeSpan.FromSeconds(PerRecordTimeoutSeconds));

                try
                {
                    var uncImagePath = _uncPathConverter.ConvertToUncPath(result.ImagePath);
                    await _teamsNotifyService.SendAsync(result, uncImagePath, recordTimeout.Token);
                    await _toolCheckResultService.MarkTeamsSentAsync(result, recordTimeout.Token);

                    _logger.LogInformation("Teams notification sent. RecordKey={RecordKey}, SFC={Sfc}, ToolId={ToolId}", result.RecordKey, result.Sfc, result.ToolId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Teams notification failed. RecordKey={RecordKey}", result.RecordKey);

                    try
                    {
                        await _toolCheckResultService.MarkTeamsFailedAsync(result, BuildFailureMessage(ex, recordTimeout), cancellationToken);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Failed to update SendErrorMessage. RecordKey={RecordKey}", result.RecordKey);
                    }
                }
            }
        }

        private int IntervalSeconds
        {
            get
            {
                return Math.Max(1, _options.IntervalSeconds);
            }
        }

        private int BatchSize
        {
            get
            {
                return Math.Max(1, _options.BatchSize);
            }
        }

        private bool ShouldStopAfterConsecutiveFailures
        {
            get
            {
                return _options.StopAfterConsecutiveCycleFailures > 0 &&
                       _consecutiveCycleFailures >= _options.StopAfterConsecutiveCycleFailures;
            }
        }

        private int PerRecordTimeoutSeconds
        {
            get
            {
                return Math.Max(1, _options.PerRecordTimeoutSeconds);
            }
        }

        private static string BuildFailureMessage(Exception exception, CancellationTokenSource recordTimeout)
        {
            return recordTimeout.IsCancellationRequested
                ? $"Per-record processing timed out. {exception.Message}"
                : exception.Message;
        }
    }
}
