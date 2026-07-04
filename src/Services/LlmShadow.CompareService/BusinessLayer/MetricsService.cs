using LlmShadow.DataLayer.Repositories;
using LlmShadow.Models.Response;
using Microsoft.Extensions.Caching.Memory;

namespace LlmShadow.CompareService.BusinessLayer;

/// <summary>Implementation of <see cref="IMetricsService"/> with short-TTL memory caching.</summary>
public sealed class MetricsService : IMetricsService
{
    private const string CacheKey = "metrics:summary";

    private readonly IRequestRepository _requestRepository;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheTtl;
    private readonly ILogger<MetricsService> _logger;

    /// <summary>Initialises the service.</summary>
    public MetricsService(
        IRequestRepository requestRepository,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<MetricsService> logger)
    {
        _requestRepository = requestRepository;
        _cache = cache;
        _logger = logger;
        _cacheTtl = TimeSpan.FromSeconds(
            configuration.GetValue("MetricsCacheTtlSeconds", 30));
    }

    /// <inheritdoc />
    public async Task<MetricsSummaryDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out MetricsSummaryDto? cached) && cached is not null)
        {
            _logger.LogDebug("Metrics served from cache");
            return cached;
        }

        _logger.LogDebug("Metrics cache miss; querying database");
        var metrics = await _requestRepository.GetMetricsAsync(cancellationToken);

        _cache.Set(CacheKey, metrics, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheTtl,
            Size = 1
        });

        return metrics;
    }
}
