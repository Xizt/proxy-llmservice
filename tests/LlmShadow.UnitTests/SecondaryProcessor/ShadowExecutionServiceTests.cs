using FluentAssertions;
using LlmShadow.Common.Options;
using LlmShadow.DataLayer.Models;
using LlmShadow.DataLayer.Repositories;
using LlmShadow.Inference;
using LlmShadow.Inference.Models;
using LlmShadow.Models.Common;
using LlmShadow.Models.Request;
using LlmShadow.SecondaryProcessor.BusinessLayer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace LlmShadow.UnitTests.SecondaryProcessor;

/// <summary>Unit tests for <see cref="ShadowExecutionService"/>.</summary>
public sealed class ShadowExecutionServiceTests
{
    private readonly Mock<IInferenceClient> _mockInference = new();
    private readonly Mock<ISecondaryResponseRepository> _mockSecondaryRepo = new();
    private readonly Mock<IRequestRepository> _mockRequestRepo = new();

    private readonly IOptions<InferenceOptions> _options = Options.Create(new InferenceOptions
    {
        CandidateModel = "llama-3-70b",
        CandidateTimeoutSeconds = 30
    });

    public ShadowExecutionServiceTests()
    {
        _mockSecondaryRepo
            .Setup(x => x.AddAsync(It.IsAny<SecondaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockRequestRepo
            .Setup(x => x.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private ShadowExecutionService CreateSut() =>
        new(_mockInference.Object, _mockSecondaryRepo.Object, _mockRequestRepo.Object,
            _options, NullLogger<ShadowExecutionService>.Instance);

    private static ShadowQueueMessage BuildMessage(string? payloadJson = null)
    {
        var payload = payloadJson ?? JsonSerializer.Serialize(new ChatRequestDto
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "Hello" }]
        });

        return new ShadowQueueMessage
        {
            RequestId = Guid.NewGuid(),
            RequestPayloadJson = payload,
            CandidateModel = "llama-3-70b"
        };
    }

    // ── success path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Success_PersistsSecondaryResponseWithContent()
    {
        // Arrange
        _mockInference
            .Setup(x => x.CompleteAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InferenceResponse { Content = """{"action":"navigate"}""", Model = "llama-3-70b" });

        SecondaryLlmResponse? saved = null;
        _mockSecondaryRepo
            .Setup(x => x.AddAsync(It.IsAny<SecondaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Callback<SecondaryLlmResponse, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().ExecuteAsync(BuildMessage(), CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        saved!.ResponseText.Should().Be("""{"action":"navigate"}""");
        saved.IsError.Should().BeFalse();
        saved.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Success_DoesNotUpdateRequestStatus()
    {
        // Arrange
        _mockInference
            .Setup(x => x.CompleteAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InferenceResponse { Content = "ok" });

        // Act
        await CreateSut().ExecuteAsync(BuildMessage(), CancellationToken.None);

        // Assert — no status update on success
        _mockRequestRepo.Verify(
            x => x.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<RequestStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Success_ForwardsMessagesToCandidateModel()
    {
        // Arrange
        InferenceRequest? capturedRequest = null;
        _mockInference
            .Setup(x => x.CompleteAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<InferenceRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new InferenceResponse { Content = "ok" });

        var message = BuildMessage();

        // Act
        await CreateSut().ExecuteAsync(message, CancellationToken.None);

        // Assert
        capturedRequest!.Model.Should().Be(message.CandidateModel);
        capturedRequest.Messages.Should().HaveCount(1);
        capturedRequest.Messages[0].Role.Should().Be("user");
        capturedRequest.Messages[0].Content.Should().Be("Hello");
    }

    // ── timeout path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CandidateTimesOut_PersistsErrorResponseWithTimedoutStatus()
    {
        // Arrange
        _mockInference
            .Setup(x => x.CompleteAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("timed out"));

        SecondaryLlmResponse? saved = null;
        _mockSecondaryRepo
            .Setup(x => x.AddAsync(It.IsAny<SecondaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Callback<SecondaryLlmResponse, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        var message = BuildMessage();

        // Act — use a non-cancelled parent token so the OperationCanceledException is treated as a timeout
        await CreateSut().ExecuteAsync(message, CancellationToken.None);

        // Assert
        saved!.IsError.Should().BeTrue();
        saved.ErrorMessage.Should().Contain("timed out");
        _mockRequestRepo.Verify(
            x => x.UpdateStatusAsync(message.RequestId, RequestStatus.Timedout, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── general failure path ─────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CandidateThrowsGenericException_PersistsErrorResponseWithFailedStatus()
    {
        // Arrange
        _mockInference
            .Setup(x => x.CompleteAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("inference endpoint unreachable"));

        SecondaryLlmResponse? saved = null;
        _mockSecondaryRepo
            .Setup(x => x.AddAsync(It.IsAny<SecondaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Callback<SecondaryLlmResponse, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        var message = BuildMessage();

        // Act
        await CreateSut().ExecuteAsync(message, CancellationToken.None);

        // Assert
        saved!.IsError.Should().BeTrue();
        saved.ErrorMessage.Should().Contain("inference endpoint unreachable");

        _mockRequestRepo.Verify(
            x => x.UpdateStatusAsync(message.RequestId, RequestStatus.SecondaryLLMResponseFailed, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── payload deserialisation failure ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_InvalidPayloadJson_TreatsAsCandidateFailure()
    {
        // Arrange — invalid JSON in the message payload
        var message = BuildMessage(payloadJson: "not valid json at all");

        SecondaryLlmResponse? saved = null;
        _mockSecondaryRepo
            .Setup(x => x.AddAsync(It.IsAny<SecondaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Callback<SecondaryLlmResponse, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        // Act — must NOT throw
        await CreateSut().Invoking(s => s.ExecuteAsync(message, CancellationToken.None))
            .Should().NotThrowAsync();

        // Assert
        saved!.IsError.Should().BeTrue();
        _mockRequestRepo.Verify(
            x => x.UpdateStatusAsync(message.RequestId, RequestStatus.SecondaryLLMResponseFailed, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NullPayloadDeserialisesTo_NullDto_TreatsAsCandidateFailure()
    {
        // Arrange — "null" is valid JSON that deserialises to null
        var message = BuildMessage(payloadJson: "null");

        SecondaryLlmResponse? saved = null;
        _mockSecondaryRepo
            .Setup(x => x.AddAsync(It.IsAny<SecondaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Callback<SecondaryLlmResponse, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().Invoking(s => s.ExecuteAsync(message, CancellationToken.None))
            .Should().NotThrowAsync();

        // Assert
        saved!.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_Success_ResponseIdMatchesMessageRequestId()
    {
        // Arrange
        var message = BuildMessage();
        _mockInference
            .Setup(x => x.CompleteAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InferenceResponse { Content = "ok" });

        SecondaryLlmResponse? saved = null;
        _mockSecondaryRepo
            .Setup(x => x.AddAsync(It.IsAny<SecondaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Callback<SecondaryLlmResponse, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().ExecuteAsync(message, CancellationToken.None);

        // Assert
        saved!.RequestId.Should().Be(message.RequestId);
    }
}
