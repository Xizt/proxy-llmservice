using FluentAssertions;
using LlmShadow.DataLayer;
using LlmShadow.DataLayer.Models;
using LlmShadow.DataLayer.Repositories;
using LlmShadow.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LlmShadow.UnitTests.Metrics;

/// <summary>Tests the metrics aggregation query via an in-memory <see cref="ShadowDbContext"/>.</summary>
public sealed class MetricsServiceTests : IDisposable
{
    private readonly ShadowDbContext _db;
    private readonly RequestRepository _sut;

    public MetricsServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShadowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ShadowDbContext(options);
        _sut = new RequestRepository(_db, NullLogger<RequestRepository>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetMetricsAsync_NoData_ReturnsZeroMetrics()
    {
        // Act
        var metrics = await _sut.GetMetricsAsync();

        // Assert
        metrics.TotalRequests.Should().Be(0);
        metrics.ExactMatchRatePercent.Should().Be(0.0);
        metrics.ShadowErrors.Should().Be(0);
        metrics.ShadowTimeouts.Should().Be(0);
    }

    [Fact]
    public async Task GetMetricsAsync_WithMatchedAndFailed_ComputesCorrectMatchRate()
    {
        // Arrange — 3 Matched, 1 Failed => 75% match rate
        _db.Requests.AddRange(
            MakeRecord(RequestStatus.Matched),
            MakeRecord(RequestStatus.Matched),
            MakeRecord(RequestStatus.Matched),
            MakeRecord(RequestStatus.Failed));
        await _db.SaveChangesAsync();

        // Act
        var metrics = await _sut.GetMetricsAsync();

        // Assert
        metrics.TotalRequests.Should().Be(4);
        metrics.EvaluatedRequests.Should().Be(4);
        metrics.ExactMatchRatePercent.Should().Be(75.0);
    }

    [Fact]
    public async Task GetMetricsAsync_WithShadowErrors_ReturnsCorrectErrorCounts()
    {
        // Arrange
        _db.Requests.AddRange(
            MakeRecord(RequestStatus.SecondaryLLMResponseFailed),
            MakeRecord(RequestStatus.SecondaryLLMResponseFailed),
            MakeRecord(RequestStatus.Timedout),
            MakeRecord(RequestStatus.Matched));
        await _db.SaveChangesAsync();

        // Act
        var metrics = await _sut.GetMetricsAsync();

        // Assert
        metrics.ShadowErrors.Should().Be(2);
        metrics.ShadowTimeouts.Should().Be(1);
        metrics.TotalRequests.Should().Be(4);
    }

    [Fact]
    public async Task GetMetricsAsync_OnlyUnevaluated_ReturnsZeroMatchRate()
    {
        // Arrange
        _db.Requests.AddRange(
            MakeRecord(RequestStatus.Created),
            MakeRecord(RequestStatus.Created));
        await _db.SaveChangesAsync();

        // Act
        var metrics = await _sut.GetMetricsAsync();

        // Assert
        metrics.ExactMatchRatePercent.Should().Be(0.0);
        metrics.EvaluatedRequests.Should().Be(0);
        metrics.TotalRequests.Should().Be(2);
    }

    [Fact]
    public async Task GetMetricsAsync_StatusBreakdown_ContainsAllPresentStatuses()
    {
        // Arrange
        _db.Requests.AddRange(
            MakeRecord(RequestStatus.Matched),
            MakeRecord(RequestStatus.Created));
        await _db.SaveChangesAsync();

        // Act
        var metrics = await _sut.GetMetricsAsync();

        // Assert
        metrics.StatusBreakdown.Should().ContainKey(RequestStatus.Matched.ToString());
        metrics.StatusBreakdown.Should().ContainKey(RequestStatus.Created.ToString());
        metrics.StatusBreakdown[RequestStatus.Matched.ToString()].Should().Be(1);
        metrics.StatusBreakdown[RequestStatus.Created.ToString()].Should().Be(1);
    }

    private static RequestRecord MakeRecord(RequestStatus status) => new()
    {
        RequestId = Guid.NewGuid(),
        Status = status,
        Model = "test-model",
        CandidateModel = "candidate-model",
        RequestPayloadJson = "{}",
        CreatedAtUtc = DateTime.UtcNow,
        ModifiedAtUtc = DateTime.UtcNow
    };
}
