using LlmShadow.DataLayer.Models;
using LlmShadow.Models.Common;
using LlmShadow.Models.Response;

namespace LlmShadow.DataLayer.Repositories;

/// <summary>Data-access contract for the <c>RequestRecord</c> aggregate, including its one-to-one response children.</summary>
public interface IRequestRepository
{
    /// <summary>
    /// Persists a new <see cref="RequestRecord"/> together with its associated <see cref="PrimaryLlmResponse"/>
    /// in a single atomic transaction.
    /// </summary>
    Task AddAsync(RequestRecord record, PrimaryLlmResponse primaryResponse, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a <see cref="RequestRecord"/> by its application-level <paramref name="requestId"/>,
    /// including both response navigation properties.
    /// Returns <c>null</c> when not found.
    /// </summary>
    Task<RequestRecord?> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a request identified by <paramref name="requestId"/>.
    /// No-ops silently when the record does not exist.
    /// </summary>
    Task UpdateStatusAsync(Guid requestId, RequestStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the evaluation outcome for a request. Sets <see cref="RequestRecord.IsMatch"/>,
    /// <see cref="RequestRecord.MatchPercentage"/>, <see cref="RequestRecord.EvaluatedAtUtc"/>,
    /// and <see cref="RequestRecord.Status"/>.
    /// </summary>
    Task UpdateEvaluationResultAsync(
        Guid requestId,
        RequestStatus status,
        bool? isMatch,
        double? matchPercentage,
        DateTime evaluatedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="batchSize"/> <see cref="RequestRecord"/>s with
    /// <see cref="RequestStatus.Created"/> status that have both a primary and a secondary response stored,
    /// ordered by <see cref="BaseEntity.CreatedAtUtc"/> ascending. Used by the evaluator worker.
    /// </summary>
    Task<IReadOnlyList<RequestRecord>> GetUnevaluatedWithBothResponsesAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes and returns the real-time metrics summary used by the CompareService.
    /// </summary>
    Task<MetricsSummaryDto> GetMetricsAsync(CancellationToken cancellationToken = default);
}
