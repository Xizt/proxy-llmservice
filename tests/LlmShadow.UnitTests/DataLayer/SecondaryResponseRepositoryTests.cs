using FluentAssertions;
using LlmShadow.DataLayer;
using LlmShadow.DataLayer.Models;
using LlmShadow.DataLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LlmShadow.UnitTests.DataLayer;

/// <summary>Unit tests for <see cref="SecondaryResponseRepository"/>.</summary>
public sealed class SecondaryResponseRepositoryTests : IDisposable
{
    private readonly ShadowDbContext _db;
    private readonly SecondaryResponseRepository _sut;

    public SecondaryResponseRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ShadowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new ShadowDbContext(options);
        _sut = new SecondaryResponseRepository(_db, NullLogger<SecondaryResponseRepository>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AddAsync_PersistsSecondaryResponse()
    {
        // Arrange — first add a parent RequestRecord (FK constraint with in-memory is not enforced,
        // but adding for correctness of cascade relationships)
        var requestId = Guid.NewGuid();

        var response = new SecondaryLlmResponse
        {
            RequestId    = requestId,
            ResponseText = """{"action":"navigate"}""",
            IsError      = false,
            LatencyMs    = 250
        };

        // Act
        await _sut.AddAsync(response);

        // Assert
        var saved = await _db.SecondaryLlmResponses
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        saved.Should().NotBeNull();
        saved!.ResponseText.Should().Be("""{"action":"navigate"}""");
        saved.IsError.Should().BeFalse();
        saved.LatencyMs.Should().Be(250);
    }

    [Fact]
    public async Task AddAsync_ErrorResponse_PersistsErrorDetails()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var response = new SecondaryLlmResponse
        {
            RequestId    = requestId,
            ResponseText = null,
            IsError      = true,
            ErrorMessage = "Model unavailable",
            LatencyMs    = 5000
        };

        // Act
        await _sut.AddAsync(response);

        // Assert
        var saved = await _db.SecondaryLlmResponses
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        saved.Should().NotBeNull();
        saved!.IsError.Should().BeTrue();
        saved.ErrorMessage.Should().Be("Model unavailable");
        saved.ResponseText.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_SetsAuditTimestamps()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);
        var requestId = Guid.NewGuid();
        var response = new SecondaryLlmResponse { RequestId = requestId, ResponseText = "ok" };

        // Act
        await _sut.AddAsync(response);

        // Assert
        var saved = await _db.SecondaryLlmResponses.FirstAsync(r => r.RequestId == requestId);
        saved.CreatedAtUtc.Should().BeAfter(before);
        saved.ModifiedAtUtc.Should().BeAfter(before);
    }

    [Fact]
    public async Task AddAsync_MultipleResponses_AllPersisted()
    {
        // Arrange
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();

        // Act
        foreach (var id in ids)
            await _sut.AddAsync(new SecondaryLlmResponse { RequestId = id, ResponseText = "ok" });

        // Assert
        var count = await _db.SecondaryLlmResponses.CountAsync();
        count.Should().Be(3);
    }
}
