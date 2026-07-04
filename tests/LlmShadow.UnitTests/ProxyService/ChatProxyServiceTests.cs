using System.Runtime.CompilerServices;
using FluentAssertions;
using LlmShadow.Common;
using LlmShadow.Common.Options;
using LlmShadow.DataLayer.Models;
using LlmShadow.DataLayer.Repositories;
using LlmShadow.Inference;
using LlmShadow.Inference.Models;
using LlmShadow.Messaging;
using LlmShadow.Models.Common;
using LlmShadow.Models.Request;
using LlmShadow.ProxyService.BusinessLayer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LlmShadow.UnitTests.ProxyService;

/// <summary>Unit tests for <see cref="ChatProxyService"/>.</summary>
public sealed class ChatProxyServiceTests
{
    private readonly Mock<IInferenceClient> _mockInference = new();
    private readonly Mock<IRequestRepository> _mockRepo = new();
    private readonly Mock<IShadowQueuePublisher> _mockQueue = new();
    private readonly Mock<ICorrelationIdAccessor> _mockCorrelation = new();
    private readonly IOptions<InferenceOptions> _options;

    public ChatProxyServiceTests()
    {
        _mockCorrelation.Setup(x => x.CorrelationId).Returns("test-correlation-id");
        _mockRepo.Setup(x => x.AddAsync(It.IsAny<RequestRecord>(), It.IsAny<PrimaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockQueue.Setup(x => x.PublishAsync(It.IsAny<ShadowQueueMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _options = Options.Create(new InferenceOptions
        {
            PrimaryModel = "gpt-4o",
            CandidateModel = "llama-3-70b",
            PrimaryTimeoutSeconds = 60,
            CandidateTimeoutSeconds = 120
        });
    }

    private ChatProxyService CreateSut() =>
        new(_mockInference.Object, _mockRepo.Object, _mockQueue.Object,
            _mockCorrelation.Object, _options, NullLogger<ChatProxyService>.Instance);

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<string> StreamChunks(
        IEnumerable<string> chunks,
        [EnumeratorCancellation] CancellationToken _ = default)
    {
        foreach (var c in chunks)
        {
            await Task.Yield();
            yield return c;
        }
    }

    private static async IAsyncEnumerable<string> ThrowingStream(
        Exception ex,
        [EnumeratorCancellation] CancellationToken _ = default)
    {
        await Task.Yield();
        throw ex;
        yield break; // unreachable; required for async iterator return type
    }

    private static ChatRequestDto BuildRequest(string? model = null) => new()
    {
        Messages = new[] { new ChatMessageDto { Role = "user", Content = "Hello" } },
        Model = model
    };

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SuccessfulStream_CallsOnChunkForEachDelta()
    {
        // Arrange
        _mockInference
            .Setup(x => x.StreamCompletionAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(StreamChunks(["Hello", " World"]));

        var sut = CreateSut();
        var received = new List<string>();

        // Act
        await sut.ExecuteAsync(BuildRequest(), async c => received.Add(c), CancellationToken.None);

        // Assert
        received.Should().Equal("Hello", " World");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulStream_PersistsRecordWithCreatedStatus()
    {
        // Arrange
        _mockInference
            .Setup(x => x.StreamCompletionAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(StreamChunks(["response"]));

        RequestRecord? savedRecord = null;
        _mockRepo
            .Setup(x => x.AddAsync(It.IsAny<RequestRecord>(), It.IsAny<PrimaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Callback<RequestRecord, PrimaryLlmResponse, CancellationToken>((r, _, _) => savedRecord = r)
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().ExecuteAsync(BuildRequest(), _ => Task.CompletedTask, CancellationToken.None);

        // Assert
        savedRecord.Should().NotBeNull();
        savedRecord!.Status.Should().Be(RequestStatus.Created);
        savedRecord.CandidateModel.Should().Be("llama-3-70b");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulStream_UsesConfiguredPrimaryModelWhenRequestHasNoModel()
    {
        // Arrange
        InferenceRequest? capturedRequest = null;
        _mockInference
            .Setup(x => x.StreamCompletionAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<InferenceRequest, CancellationToken>((r, _) => capturedRequest = r)
            .Returns(StreamChunks([]));

        // Act
        await CreateSut().ExecuteAsync(BuildRequest(model: null), _ => Task.CompletedTask, CancellationToken.None);

        // Assert
        capturedRequest!.Model.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task ExecuteAsync_RequestHasModelOverride_UsesRequestModel()
    {
        // Arrange
        InferenceRequest? capturedRequest = null;
        _mockInference
            .Setup(x => x.StreamCompletionAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<InferenceRequest, CancellationToken>((r, _) => capturedRequest = r)
            .Returns(StreamChunks([]));

        // Act
        await CreateSut().ExecuteAsync(BuildRequest(model: "custom-model"), _ => Task.CompletedTask, CancellationToken.None);

        // Assert
        capturedRequest!.Model.Should().Be("custom-model");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulStream_PublishesShadowMessage()
    {
        // Arrange
        _mockInference
            .Setup(x => x.StreamCompletionAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(StreamChunks(["ok"]));

        // Act
        await CreateSut().ExecuteAsync(BuildRequest(), _ => Task.CompletedTask, CancellationToken.None);

        // Allow the fire-and-forget task to complete
        await Task.Delay(50);

        // Assert
        _mockQueue.Verify(x => x.PublishAsync(It.IsAny<ShadowQueueMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PrimaryLlmThrows_SavesFailedStatusAndRethrows()
    {
        // Arrange
        var error = new HttpRequestException("inference error");
        _mockInference
            .Setup(x => x.StreamCompletionAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ThrowingStream(error));

        RequestRecord? savedRecord = null;
        _mockRepo
            .Setup(x => x.AddAsync(It.IsAny<RequestRecord>(), It.IsAny<PrimaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Callback<RequestRecord, PrimaryLlmResponse, CancellationToken>((r, _, _) => savedRecord = r)
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act & Assert
        await sut.Invoking(s => s.ExecuteAsync(BuildRequest(), _ => Task.CompletedTask, CancellationToken.None))
            .Should().ThrowAsync<HttpRequestException>();

        savedRecord!.Status.Should().Be(RequestStatus.PrimaryLLMResponseFailed);
    }

    [Fact]
    public async Task ExecuteAsync_PrimaryLlmThrows_DoesNotPublishShadowMessage()
    {
        // Arrange
        _mockInference
            .Setup(x => x.StreamCompletionAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ThrowingStream(new Exception("boom")));

        var sut = CreateSut();

        // Act — suppress the rethrown exception
        try { await sut.ExecuteAsync(BuildRequest(), _ => Task.CompletedTask, CancellationToken.None); }
        catch { /* expected */ }

        await Task.Delay(50);

        // Assert
        _mockQueue.Verify(x => x.PublishAsync(It.IsAny<ShadowQueueMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_DatabaseThrowsAfterSuccess_ExceptionIsSwallowedNotRethrown()
    {
        // Arrange
        _mockInference
            .Setup(x => x.StreamCompletionAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(StreamChunks(["ok"]));

        _mockRepo
            .Setup(x => x.AddAsync(It.IsAny<RequestRecord>(), It.IsAny<PrimaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB offline"));

        var sut = CreateSut();

        // Act & Assert — DB failure must not surface to the caller
        await sut.Invoking(s => s.ExecuteAsync(BuildRequest(), _ => Task.CompletedTask, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_QueuePublishThrowsAfterSuccess_ExceptionIsSwallowed()
    {
        // Arrange
        _mockInference
            .Setup(x => x.StreamCompletionAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(StreamChunks(["ok"]));

        _mockQueue
            .Setup(x => x.PublishAsync(It.IsAny<ShadowQueueMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis offline"));

        var sut = CreateSut();

        // Act & Assert — queue failure must not surface
        await sut.Invoking(s => s.ExecuteAsync(BuildRequest(), _ => Task.CompletedTask, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulStream_PrimaryResponseContainsBufferedText()
    {
        // Arrange
        _mockInference
            .Setup(x => x.StreamCompletionAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(StreamChunks(["Hello", " World"]));

        PrimaryLlmResponse? savedResponse = null;
        _mockRepo
            .Setup(x => x.AddAsync(It.IsAny<RequestRecord>(), It.IsAny<PrimaryLlmResponse>(), It.IsAny<CancellationToken>()))
            .Callback<RequestRecord, PrimaryLlmResponse, CancellationToken>((_, p, _) => savedResponse = p)
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().ExecuteAsync(BuildRequest(), _ => Task.CompletedTask, CancellationToken.None);

        // Assert
        savedResponse!.ResponseText.Should().Be("Hello World");
        savedResponse.IsError.Should().BeFalse();
    }
}
