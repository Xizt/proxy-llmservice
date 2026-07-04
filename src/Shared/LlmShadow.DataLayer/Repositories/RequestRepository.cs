using LlmShadow.DataLayer.Models;
using LlmShadow.Models.Common;
using LlmShadow.Models.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LlmShadow.DataLayer.Repositories;

/// <summary>EF Core implementation of <see cref="IRequestRepository"/>.</summary>
public sealed class RequestRepository : IRequestRepository
{
    private readonly ShadowDbContext _db;
    private readonly ILogger<RequestRepository> _logger;

    /// <summary>Initialises the repository with a scoped <see cref="ShadowDbContext"/>.</summary>
    public RequestRepository(ShadowDbContext db, ILogger<RequestRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddAsync(
        RequestRecord record,
        PrimaryLlmResponse primaryResponse,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Requests.Add(record);
            _db.PrimaryLlmResponses.Add(primaryResponse);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist request {RequestId}", record.RequestId);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<RequestRecord?> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        _db.Requests
            .Include(r => r.PrimaryResponse)
            .Include(r => r.SecondaryResponse)
            .FirstOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);

    /// <inheritdoc />
    public async Task UpdateStatusAsync(Guid requestId, RequestStatus status, CancellationToken cancellationToken = default)
    {
        var record = await _db.Requests
            .FirstOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("UpdateStatus: request {RequestId} not found", requestId);
            return;
        }

        record.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateEvaluationResultAsync(
        Guid requestId,
        RequestStatus status,
        bool? isMatch,
        double? matchPercentage,
        DateTime evaluatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.Requests
            .FirstOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("UpdateEvaluationResult: request {RequestId} not found", requestId);
            return;
        }

        record.Status = status;
        record.IsMatch = isMatch;
        record.MatchPercentage = matchPercentage;
        record.EvaluatedAtUtc = evaluatedAtUtc;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RequestRecord>> GetUnevaluatedWithBothResponsesAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _db.Requests
            .Include(r => r.PrimaryResponse)
            .Include(r => r.SecondaryResponse)
            .Where(r => r.Status == RequestStatus.Created
                     && r.PrimaryResponse != null
                     && r.SecondaryResponse != null)
            .OrderBy(r => r.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MetricsSummaryDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _db.Requests
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = (long)g.Count() })
            .ToListAsync(cancellationToken);

        var breakdown = groups.ToDictionary(
            g => g.Status.ToString(),
            g => g.Count);

        var total = groups.Sum(g => g.Count);
        var shadowErrors = groups.Where(g => g.Status == RequestStatus.SecondaryLLMResponseFailed).Sum(g => g.Count);
        var shadowTimeouts = groups.Where(g => g.Status == RequestStatus.Timedout).Sum(g => g.Count);

        var matched = groups.Where(g => g.Status == RequestStatus.Matched).Sum(g => g.Count);
        var failed = groups.Where(g => g.Status == RequestStatus.Failed).Sum(g => g.Count);
        var evaluated = matched + failed;
        var matchRate = evaluated > 0 ? (double)matched / evaluated * 100.0 : 0.0;

        return new MetricsSummaryDto
        {
            TotalRequests = total,
            ShadowErrors = shadowErrors,
            ShadowTimeouts = shadowTimeouts,
            ExactMatchRatePercent = Math.Round(matchRate, 2),
            EvaluatedRequests = evaluated,
            StatusBreakdown = breakdown
        };
    }
}
