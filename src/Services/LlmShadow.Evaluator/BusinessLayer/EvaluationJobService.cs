using LlmShadow.DataLayer.Repositories;
using LlmShadow.Evaluation;
using LlmShadow.Models.Common;

namespace LlmShadow.Evaluator.BusinessLayer;

/// <summary>Implementation of <see cref="IEvaluationJobService"/>.</summary>
public sealed class EvaluationJobService : IEvaluationJobService
{
    private readonly IRequestRepository _requestRepo;
    private readonly IHeuristicEvaluator _evaluator;
    private readonly ILogger<EvaluationJobService> _logger;

    /// <summary>Initialises the service with required dependencies.</summary>
    public EvaluationJobService(
        IRequestRepository requestRepo,
        IHeuristicEvaluator evaluator,
        ILogger<EvaluationJobService> logger)
    {
        _requestRepo = requestRepo;
        _evaluator = evaluator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RunBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var records = await _requestRepo.GetUnevaluatedWithBothResponsesAsync(batchSize, cancellationToken);

        if (records.Count == 0)
        {
            _logger.LogDebug("Evaluator: no unevaluated records found in this cycle");
            return;
        }

        _logger.LogInformation("Evaluator: processing {Count} record(s)", records.Count);

        foreach (var record in records)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var primaryText = record.PrimaryResponse?.ResponseText ?? string.Empty;
                var secondaryText = record.SecondaryResponse?.ResponseText ?? string.Empty;

                var result = _evaluator.Evaluate(primaryText, secondaryText);

                RequestStatus status;
                bool? isMatch;
                double? matchPct;

                if (result.IsSuccess)
                {
                    status = result.IsMatch ? RequestStatus.Matched : RequestStatus.Failed;
                    isMatch = result.IsMatch;
                    matchPct = result.MatchPercentage;
                }
                else
                {
                    status = RequestStatus.Failed;
                    isMatch = false;
                    matchPct = 0.0;

                    _logger.LogWarning(
                        "Evaluation for request {RequestId} failed: {Reason}",
                        record.RequestId, result.FailureReason);
                }

                await _requestRepo.UpdateEvaluationResultAsync(
                    record.RequestId,
                    status,
                    isMatch,
                    matchPct,
                    DateTime.UtcNow,
                    cancellationToken);

                _logger.LogInformation(
                    "Request {RequestId} evaluated: status={Status} isMatch={IsMatch}",
                    record.RequestId, status, isMatch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Evaluation failed for request {RequestId}", record.RequestId);
            }
        }
    }
}
