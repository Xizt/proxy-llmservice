using FluentAssertions;
using LlmShadow.DataLayer;
using LlmShadow.DataLayer.Models;
using LlmShadow.DataLayer.Repositories;
using LlmShadow.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LlmShadow.UnitTests.DataLayer;

/// <summary>
/// Additional unit tests for <see cref="RequestRepository"/> covering query, update, and
/// soft-delete behaviours using an in-memory <see cref="ShadowDbContext"/>.
/// </summary>
public sealed class RequestRepositoryAdditionalTests : IDisposable
{
    private readonly ShadowDbContext _db;
    private readonly RequestRepository _sut;

    public RequestRepositoryAdditionalTests()
    {
        var options = new DbContextOptionsBuilder<ShadowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new ShadowDbContext(options);
        _sut = new RequestRepository(_db, NullLogger<RequestRepository>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static RequestRecord MakeRecord(RequestStatus status = RequestStatus.Created) => new()
    {
        RequestId          = Guid.NewGuid(),
        Status             = status,
        Model              = "primary-model",
        CandidateModel     = "candidate-model",
        RequestPayloadJson = "{}",
        CreatedAtUtc       = DateTime.UtcNow,
        ModifiedAtUtc      = DateTime.UtcNow
    };

    private static PrimaryLlmResponse MakePrimary(Guid requestId) => new()
    {
        RequestId    = requestId,
        ResponseText = """{"action":"navigate"}""",
        IsError      = false,
        LatencyMs    = 100
    };

    private static SecondaryLlmResponse MakeSecondary(Guid requestId) => new()
    {
        RequestId    = requestId,
        ResponseText = """{"action":"navigate"}""",
        IsError      = false,
        LatencyMs    = 200
    };

    // ── GetByRequestIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByRequestIdAsync_RecordNotFound_ReturnsNull()
    {
        var result = await _sut.GetByRequestIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByRequestIdAsync_RecordExists_ReturnsRecord()
    {
        // Arrange
        var record = MakeRecord();
        _db.Requests.Add(record);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetByRequestIdAsync(record.RequestId);

        // Assert
        result.Should().NotBeNull();
        result!.RequestId.Should().Be(record.RequestId);
        result.Model.Should().Be("primary-model");
    }

    [Fact]
    public async Task GetByRequestIdAsync_RecordWithBothResponses_IncludesNavigationProperties()
    {
        // Arrange
        var record = MakeRecord();
        _db.Requests.Add(record);
        _db.PrimaryLlmResponses.Add(MakePrimary(record.RequestId));
        _db.SecondaryLlmResponses.Add(MakeSecondary(record.RequestId));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetByRequestIdAsync(record.RequestId);

        // Assert
        result!.PrimaryResponse.Should().NotBeNull();
        result.SecondaryResponse.Should().NotBeNull();
    }

    // ── UpdateStatusAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_RecordExists_UpdatesStatus()
    {
        // Arrange
        var record = MakeRecord(RequestStatus.Created);
        _db.Requests.Add(record);
        await _db.SaveChangesAsync();

        // Act
        await _sut.UpdateStatusAsync(record.RequestId, RequestStatus.Timedout);

        // Assert
        var updated = await _db.Requests.FirstAsync(r => r.RequestId == record.RequestId);
        updated.Status.Should().Be(RequestStatus.Timedout);
    }

    [Fact]
    public async Task UpdateStatusAsync_RecordNotFound_DoesNotThrow()
    {
        // Act & Assert
        await _sut.Invoking(r => r.UpdateStatusAsync(Guid.NewGuid(), RequestStatus.Failed))
            .Should().NotThrowAsync();
    }

    // ── UpdateEvaluationResultAsync ───────────────────────────────────────────

    [Fact]
    public async Task UpdateEvaluationResultAsync_RecordExists_UpdatesAllEvaluationFields()
    {
        // Arrange
        var record = MakeRecord();
        _db.Requests.Add(record);
        await _db.SaveChangesAsync();

        var evaluatedAt = DateTime.UtcNow;

        // Act
        await _sut.UpdateEvaluationResultAsync(
            record.RequestId,
            RequestStatus.Matched,
            isMatch: true,
            matchPercentage: 100.0,
            evaluatedAtUtc: evaluatedAt);

        // Assert
        var updated = await _db.Requests.FirstAsync(r => r.RequestId == record.RequestId);
        updated.Status.Should().Be(RequestStatus.Matched);
        updated.IsMatch.Should().BeTrue();
        updated.MatchPercentage.Should().Be(100.0);
        updated.EvaluatedAtUtc.Should().BeCloseTo(evaluatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateEvaluationResultAsync_RecordExists_CanSetMismatchResult()
    {
        // Arrange
        var record = MakeRecord();
        _db.Requests.Add(record);
        await _db.SaveChangesAsync();

        // Act
        await _sut.UpdateEvaluationResultAsync(
            record.RequestId,
            RequestStatus.Failed,
            isMatch: false,
            matchPercentage: 0.0,
            evaluatedAtUtc: DateTime.UtcNow);

        // Assert
        var updated = await _db.Requests.FirstAsync(r => r.RequestId == record.RequestId);
        updated.Status.Should().Be(RequestStatus.Failed);
        updated.IsMatch.Should().BeFalse();
        updated.MatchPercentage.Should().Be(0.0);
    }

    [Fact]
    public async Task UpdateEvaluationResultAsync_RecordNotFound_DoesNotThrow()
    {
        await _sut.Invoking(r => r.UpdateEvaluationResultAsync(
            Guid.NewGuid(), RequestStatus.Matched, true, 100.0, DateTime.UtcNow))
            .Should().NotThrowAsync();
    }

    // ── GetUnevaluatedWithBothResponsesAsync ──────────────────────────────────

    [Fact]
    public async Task GetUnevaluatedWithBothResponsesAsync_RecordWithBothResponses_ReturnsIt()
    {
        // Arrange
        var record = MakeRecord();
        _db.Requests.Add(record);
        _db.PrimaryLlmResponses.Add(MakePrimary(record.RequestId));
        _db.SecondaryLlmResponses.Add(MakeSecondary(record.RequestId));
        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.GetUnevaluatedWithBothResponsesAsync(10);

        // Assert
        results.Should().HaveCount(1);
        results[0].RequestId.Should().Be(record.RequestId);
    }

    [Fact]
    public async Task GetUnevaluatedWithBothResponsesAsync_RecordMissingSecondaryResponse_NotReturned()
    {
        // Arrange — only primary, no secondary
        var record = MakeRecord();
        _db.Requests.Add(record);
        _db.PrimaryLlmResponses.Add(MakePrimary(record.RequestId));
        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.GetUnevaluatedWithBothResponsesAsync(10);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnevaluatedWithBothResponsesAsync_AlreadyEvaluatedRecord_NotReturned()
    {
        // Arrange — Matched status means it was already evaluated
        var record = MakeRecord(RequestStatus.Matched);
        _db.Requests.Add(record);
        _db.PrimaryLlmResponses.Add(MakePrimary(record.RequestId));
        _db.SecondaryLlmResponses.Add(MakeSecondary(record.RequestId));
        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.GetUnevaluatedWithBothResponsesAsync(10);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnevaluatedWithBothResponsesAsync_RespectsBatchSize()
    {
        // Arrange — add 5 eligible records
        for (var i = 0; i < 5; i++)
        {
            var record = MakeRecord();
            _db.Requests.Add(record);
            _db.PrimaryLlmResponses.Add(MakePrimary(record.RequestId));
            _db.SecondaryLlmResponses.Add(MakeSecondary(record.RequestId));
        }
        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.GetUnevaluatedWithBothResponsesAsync(batchSize: 3);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetUnevaluatedWithBothResponsesAsync_NoEligibleRecords_ReturnsEmpty()
    {
        var results = await _sut.GetUnevaluatedWithBothResponsesAsync(10);
        results.Should().BeEmpty();
    }
}
