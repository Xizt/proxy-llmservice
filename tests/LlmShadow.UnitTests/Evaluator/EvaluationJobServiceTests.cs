using FluentAssertions;
using LlmShadow.DataLayer.Models;
using LlmShadow.DataLayer.Repositories;
using LlmShadow.Evaluation;
using LlmShadow.Evaluation.Models;
using LlmShadow.Evaluator.BusinessLayer;
using LlmShadow.Models.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LlmShadow.UnitTests.Evaluator;

/// <summary>Unit tests for <see cref="EvaluationJobService"/>.</summary>
public sealed class EvaluationJobServiceTests
{
    private readonly Mock<IRequestRepository> _mockRepo = new();
    private readonly Mock<IHeuristicEvaluator> _mockEvaluator = new();

    private EvaluationJobService CreateSut() =>
        new(_mockRepo.Object, _mockEvaluator.Object, NullLogger<EvaluationJobService>.Instance);

    private static RequestRecord MakeRecord(
        string primaryText = """{"action":"navigate"}""",
        string secondaryText = """{"action":"navigate"}""") => new()
    {
        RequestId = Guid.NewGuid(),
        Status = RequestStatus.Created,
        Model = "primary-model",
        CandidateModel = "candidate-model",
        RequestPayloadJson = "{}",
        PrimaryResponse   = new PrimaryLlmResponse   { ResponseText = primaryText,   IsError = false },
        SecondaryResponse = new SecondaryLlmResponse  { ResponseText = secondaryText, IsError = false }
    };

    [Fact]
    public async Task RunBatchAsync_EmptyBatch_DoesNotCallEvaluator()
    {
        // Arrange
        _mockRepo.Setup(x => x.GetUnevaluatedWithBothResponsesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RequestRecord>());

        // Act
        await CreateSut().RunBatchAsync(10, CancellationToken.None);

        // Assert
        _mockEvaluator.Verify(x => x.Evaluate(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockRepo.Verify(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_EvaluatorReturnsMatch_UpdatesWithMatchedStatus()
    {
        // Arrange
        var record = MakeRecord();
        _mockRepo.Setup(x => x.GetUnevaluatedWithBothResponsesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { record });

        _mockEvaluator.Setup(x => x.Evaluate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new EvaluationResult { IsMatch = true, MatchPercentage = 100.0 });

        _mockRepo.Setup(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().RunBatchAsync(10, CancellationToken.None);

        // Assert
        _mockRepo.Verify(x => x.UpdateEvaluationResultAsync(
            record.RequestId, RequestStatus.Matched, true, 100.0,
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunBatchAsync_EvaluatorReturnsMismatch_UpdatesWithFailedStatus()
    {
        // Arrange
        var record = MakeRecord(secondaryText: """{"action":"search"}""");
        _mockRepo.Setup(x => x.GetUnevaluatedWithBothResponsesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { record });

        _mockEvaluator.Setup(x => x.Evaluate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new EvaluationResult { IsMatch = false, MatchPercentage = 0.0 });

        _mockRepo.Setup(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().RunBatchAsync(10, CancellationToken.None);

        // Assert
        _mockRepo.Verify(x => x.UpdateEvaluationResultAsync(
            record.RequestId, RequestStatus.Failed, false, 0.0,
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunBatchAsync_EvaluatorReturnsFailure_UpdatesWithFailedStatusAndIsMatchFalse()
    {
        // Arrange
        var record = MakeRecord(primaryText: "not json", secondaryText: "also not json");
        _mockRepo.Setup(x => x.GetUnevaluatedWithBothResponsesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { record });

        _mockEvaluator.Setup(x => x.Evaluate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(EvaluationResult.Failure("Invalid JSON"));

        _mockRepo.Setup(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().RunBatchAsync(10, CancellationToken.None);

        // Assert
        _mockRepo.Verify(x => x.UpdateEvaluationResultAsync(
            record.RequestId, RequestStatus.Failed, false, 0.0,
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunBatchAsync_MultipleBatchItems_EachItemEvaluatedAndUpdated()
    {
        // Arrange
        var records = new[]
        {
            MakeRecord(),
            MakeRecord(secondaryText: """{"action":"search"}"""),
            MakeRecord()
        };

        _mockRepo.Setup(x => x.GetUnevaluatedWithBothResponsesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        _mockEvaluator.SetupSequence(x => x.Evaluate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new EvaluationResult { IsMatch = true,  MatchPercentage = 100.0 })
            .Returns(new EvaluationResult { IsMatch = false, MatchPercentage = 0.0   })
            .Returns(new EvaluationResult { IsMatch = true,  MatchPercentage = 100.0 });

        _mockRepo.Setup(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().RunBatchAsync(10, CancellationToken.None);

        // Assert — all three records updated
        _mockRepo.Verify(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task RunBatchAsync_RepositoryThrowsOnUpdate_LogsErrorAndContinues()
    {
        // Arrange — two records; first update throws, second should still be processed
        var records = new[] { MakeRecord(), MakeRecord() };

        _mockRepo.Setup(x => x.GetUnevaluatedWithBothResponsesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        _mockEvaluator.Setup(x => x.Evaluate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new EvaluationResult { IsMatch = true, MatchPercentage = 100.0 });

        _mockRepo.SetupSequence(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error on first"))
            .Returns(Task.CompletedTask);

        // Act — must not throw
        await CreateSut().Invoking(s => s.RunBatchAsync(10, CancellationToken.None))
            .Should().NotThrowAsync();

        // Assert — second record was still attempted
        _mockRepo.Verify(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunBatchAsync_CancellationRequested_StopsProcessingEarly()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var records = new[] { MakeRecord(), MakeRecord(), MakeRecord() };
        _mockRepo.Setup(x => x.GetUnevaluatedWithBothResponsesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        var callCount = 0;
        _mockEvaluator.Setup(x => x.Evaluate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1) cts.Cancel();
                return new EvaluationResult { IsMatch = true, MatchPercentage = 100.0 };
            });

        _mockRepo.Setup(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().RunBatchAsync(10, cts.Token);

        // Assert — cancellation after first record means only the first was fully processed
        _mockRepo.Verify(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunBatchAsync_RecordWithNullPrimaryResponseText_TreatsAsEmptyString()
    {
        // Arrange — PrimaryResponse.ResponseText is null
        var record = MakeRecord();
        record.PrimaryResponse!.ResponseText = null;

        _mockRepo.Setup(x => x.GetUnevaluatedWithBothResponsesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { record });

        string? capturedPrimary = null;
        _mockEvaluator.Setup(x => x.Evaluate(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((p, _) => capturedPrimary = p)
            .Returns(EvaluationResult.Failure("empty"));

        _mockRepo.Setup(x => x.UpdateEvaluationResultAsync(
            It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<bool?>(),
            It.IsAny<double?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().RunBatchAsync(10, CancellationToken.None);

        // Assert
        capturedPrimary.Should().Be(string.Empty);
    }
}
