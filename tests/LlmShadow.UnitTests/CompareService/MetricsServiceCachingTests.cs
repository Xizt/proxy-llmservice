using FluentAssertions;
using LlmShadow.CompareService.BusinessLayer;
using LlmShadow.DataLayer.Repositories;
using LlmShadow.Models.Response;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LlmShadow.UnitTests.CompareService;

/// <summary>Unit tests for <see cref="MetricsService"/> — focuses on caching behaviour.</summary>
public sealed class MetricsServiceCachingTests : IDisposable
{
    private readonly Mock<IRequestRepository> _mockRepo = new();
    private readonly MemoryCache _cache;

    private static readonly MetricsSummaryDto SampleMetrics = new()
    {
        TotalRequests = 10,
        EvaluatedRequests = 8,
        ExactMatchRatePercent = 75.0,
        ShadowErrors = 1,
        ShadowTimeouts = 0,
        StatusBreakdown = new Dictionary<string, long> { ["Matched"] = 6, ["Failed"] = 2 }
    };

    public MetricsServiceCachingTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    }

    public void Dispose() => _cache.Dispose();

    private MetricsService CreateSut(int cacheTtlSeconds = 30)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MetricsCacheTtlSeconds"] = cacheTtlSeconds.ToString()
            })
            .Build();

        return new MetricsService(_mockRepo.Object, _cache, config, NullLogger<MetricsService>.Instance);
    }

    [Fact]
    public async Task GetMetricsAsync_CacheMiss_CallsRepository()
    {
        // Arrange
        _mockRepo.Setup(x => x.GetMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleMetrics);

        // Act
        var result = await CreateSut().GetMetricsAsync();

        // Assert
        _mockRepo.Verify(x => x.GetMetricsAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.TotalRequests.Should().Be(10);
    }

    [Fact]
    public async Task GetMetricsAsync_CalledTwice_RepositoryCalledOnlyOnce()
    {
        // Arrange
        _mockRepo.Setup(x => x.GetMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleMetrics);

        var sut = CreateSut();

        // Act
        await sut.GetMetricsAsync();
        await sut.GetMetricsAsync();

        // Assert — second call served from cache
        _mockRepo.Verify(x => x.GetMetricsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMetricsAsync_CacheHit_ReturnsCachedValue()
    {
        // Arrange
        _mockRepo.SetupSequence(x => x.GetMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleMetrics with { TotalRequests = 10 })
            .ReturnsAsync(SampleMetrics with { TotalRequests = 999 });

        var sut = CreateSut();

        // Act
        var first  = await sut.GetMetricsAsync();
        var second = await sut.GetMetricsAsync();

        // Assert — cached result returned, not the new DB value
        first.TotalRequests.Should().Be(10);
        second.TotalRequests.Should().Be(10);
    }

    [Fact]
    public async Task GetMetricsAsync_DifferentServiceInstances_ShareCache()
    {
        // Arrange — both instances share the same MemoryCache
        _mockRepo.Setup(x => x.GetMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleMetrics);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MetricsCacheTtlSeconds"] = "30" })
            .Build();

        var sut1 = new MetricsService(_mockRepo.Object, _cache, config, NullLogger<MetricsService>.Instance);
        var sut2 = new MetricsService(_mockRepo.Object, _cache, config, NullLogger<MetricsService>.Instance);

        // Act
        await sut1.GetMetricsAsync();
        await sut2.GetMetricsAsync();

        // Assert — shared cache means the repo is called only once across both instances
        _mockRepo.Verify(x => x.GetMetricsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMetricsAsync_WithMinimalPositiveTtl_StillReturnsData()
    {
        // Arrange — TTL = 1 second is the minimum positive value allowed by MemoryCacheEntryOptions
        _mockRepo.Setup(x => x.GetMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleMetrics);

        var sut = CreateSut(cacheTtlSeconds: 1);

        // Act
        var result = await sut.GetMetricsAsync();

        // Assert — data is returned correctly with a short TTL
        result.Should().NotBeNull();
        result.TotalRequests.Should().Be(10);
        result.ExactMatchRatePercent.Should().Be(75.0);
    }
}
