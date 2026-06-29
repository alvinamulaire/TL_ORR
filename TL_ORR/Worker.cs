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
        private readonly WorkerOptions _options;

        public Worker(
            ILogger<Worker> logger,
            IToolCheckResultService toolCheckResultService,
            ITeamsNotifyService teamsNotifyService,
            IUncPathConverter uncPathConverter,
            IOptions<WorkerOptions> options)
        {
            _logger = logger;
            _toolCheckResultService = toolCheckResultService;
            _teamsNotifyService = teamsNotifyService;
            _uncPathConverter = uncPathConverter;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Teams NG notification worker started. IntervalSeconds={IntervalSeconds}, BatchSize={BatchSize}",
                IntervalSeconds,
                BatchSize);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingResultsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker cycle failed. The next cycle will retry.");
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
                try
                {
                    var uncImagePath = _uncPathConverter.ConvertToUncPath(result.ImagePath);
                    await _teamsNotifyService.SendAsync(result, uncImagePath, cancellationToken);
                    await _toolCheckResultService.MarkTeamsSentAsync(result.Id, cancellationToken);

                    _logger.LogInformation("Teams notification sent. ID={Id}, SFC={Sfc}, ToolId={ToolId}", result.Id, result.Sfc, result.ToolId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Teams notification failed. ID={Id}", result.Id);

                    try
                    {
                        await _toolCheckResultService.MarkTeamsFailedAsync(result.Id, ex.Message, cancellationToken);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Failed to update SendErrorMessage. ID={Id}", result.Id);
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
    }
}
