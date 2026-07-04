using LlmShadow.Common.Options;
using LlmShadow.Messaging;
using LlmShadow.Models.Common;
using LlmShadow.SecondaryProcessor.BusinessLayer;
using Microsoft.Extensions.Options;

namespace LlmShadow.SecondaryProcessor.Workers;

/// <summary>
/// Long-running background worker that reads messages from the Redis shadow queue and
/// dispatches them to <see cref="IShadowExecutionService"/> for candidate LLM execution.
/// Uses XREADGROUP for at-least-once delivery and XAUTOCLAIM to recover stale pending messages.
/// </summary>
public sealed class ShadowProcessorWorker : BackgroundService
{
    private readonly IShadowQueueConsumer _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisOptions _redisOptions;
    private readonly ProcessorOptions _processorOptions;
    private readonly ILogger<ShadowProcessorWorker> _logger;

    // Interval between XAUTOCLAIM runs for recovering stale pending messages.
    private static readonly TimeSpan ClaimInterval = TimeSpan.FromMinutes(5);

    /// <summary>Initialises the worker.</summary>
    public ShadowProcessorWorker(
        IShadowQueueConsumer consumer,
        IServiceScopeFactory scopeFactory,
        IOptions<RedisOptions> redisOptions,
        IOptions<ProcessorOptions> processorOptions,
        ILogger<ShadowProcessorWorker> logger)
    {
        _consumer = consumer;
        _scopeFactory = scopeFactory;
        _redisOptions = redisOptions.Value;
        _processorOptions = processorOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ShadowProcessorWorker starting");

        await _consumer.EnsureConsumerGroupAsync(stoppingToken);

        var nextClaimTime = DateTime.UtcNow + ClaimInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Re-claim stale pending messages on a periodic basis.
                if (DateTime.UtcNow >= nextClaimTime)
                {
                    await ProcessBatchAsync(
                        await _consumer.ClaimStalePendingAsync(_redisOptions.BatchSize, stoppingToken),
                        stoppingToken);
                    nextClaimTime = DateTime.UtcNow + ClaimInterval;
                }

                // Read new undelivered messages.
                var batch = await _consumer.ReadBatchAsync(_redisOptions.BatchSize, stoppingToken);
                if (batch.Count == 0) continue;

                await ProcessBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in ShadowProcessorWorker loop; retrying after 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("ShadowProcessorWorker stopped");
    }

    private async Task ProcessBatchAsync(
        IReadOnlyList<(string MessageId, ShadowQueueMessage Message)> batch,
        CancellationToken stoppingToken)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _processorOptions.MaxDegreeOfParallelism,
            CancellationToken = stoppingToken
        };

        await Parallel.ForEachAsync(batch, parallelOptions, async (item, ct) =>
        {
            var (messageId, message) = item;

            if (message.RetryCount >= _redisOptions.MaxRetryCount)
            {
                _logger.LogWarning(
                    "Message {MessageId} exceeded max retries ({Max}); dead-lettering",
                    messageId, _redisOptions.MaxRetryCount);
                await _consumer.DeadLetterAsync(messageId, message, ct);
                return;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IShadowExecutionService>();
                await service.ExecuteAsync(message, ct);
                await _consumer.AcknowledgeAsync(messageId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process shadow message {MessageId} for request {RequestId} (attempt {Attempt})",
                    messageId, message.RequestId, message.RetryCount + 1);
                // Message stays pending; XAUTOCLAIM will re-deliver it after threshold.
            }
        });
    }
}
