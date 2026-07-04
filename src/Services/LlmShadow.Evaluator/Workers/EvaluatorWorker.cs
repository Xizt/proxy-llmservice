using LlmShadow.Common.Options;
using LlmShadow.Evaluator.BusinessLayer;
using Microsoft.Extensions.Options;

namespace LlmShadow.Evaluator.Workers;

/// <summary>
/// Scheduled background worker that periodically runs a batch of heuristic evaluations
/// over newly completed shadow request pairs.
/// </summary>
public sealed class EvaluatorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EvaluatorOptions _options;
    private readonly ILogger<EvaluatorWorker> _logger;

    /// <summary>Initialises the worker.</summary>
    public EvaluatorWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<EvaluatorOptions> options,
        ILogger<EvaluatorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "EvaluatorWorker starting with interval {Interval}s and batch size {Batch}",
            _options.IntervalSeconds, _options.BatchSize);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var jobService = scope.ServiceProvider.GetRequiredService<IEvaluationJobService>();
                await jobService.RunBatchAsync(_options.BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in EvaluatorWorker cycle");
            }
        }

        _logger.LogInformation("EvaluatorWorker stopped");
    }
}
