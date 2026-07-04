using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LlmShadow.Common;
using LlmShadow.Common.Options;
using LlmShadow.DataLayer.Models;
using LlmShadow.DataLayer.Repositories;
using LlmShadow.Inference;
using LlmShadow.Inference.Models;
using LlmShadow.Messaging;
using LlmShadow.Models.Common;
using LlmShadow.Models.Request;
using Microsoft.Extensions.Options;

namespace LlmShadow.ProxyService.BusinessLayer;

/// <summary>Implementation of <see cref="IChatProxyService"/>.</summary>
public sealed class ChatProxyService : IChatProxyService
{
    private readonly IInferenceClient _inferenceClient;
    private readonly IRequestRepository _requestRepository;
    private readonly IShadowQueuePublisher _queuePublisher;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly InferenceOptions _inferenceOptions;
    private readonly ILogger<ChatProxyService> _logger;

    /// <summary>Initialises the service with all required dependencies.</summary>
    public ChatProxyService(
        IInferenceClient inferenceClient,
        IRequestRepository requestRepository,
        IShadowQueuePublisher queuePublisher,
        ICorrelationIdAccessor correlationIdAccessor,
        IOptions<InferenceOptions> inferenceOptions,
        ILogger<ChatProxyService> logger)
    {
        _inferenceClient = inferenceClient;
        _requestRepository = requestRepository;
        _queuePublisher = queuePublisher;
        _correlationIdAccessor = correlationIdAccessor;
        _inferenceOptions = inferenceOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        ChatRequestDto request,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid();
        var payloadJson = JsonSerializer.Serialize(request);
        var primaryModel = string.IsNullOrWhiteSpace(request.Model) ? _inferenceOptions.PrimaryModel : request.Model;

        var sw = Stopwatch.StartNew();
        var fullResponse = new StringBuilder();
        var primaryFailed = false;
        string? primaryError = null;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_inferenceOptions.PrimaryTimeoutSeconds));

        try
        {
            var inferRequest = new InferenceRequest
            {
                Model = primaryModel,
                Messages = request.Messages
                    .Select(m => new InferenceChatMessage { Role = m.Role, Content = m.Content })
                    .ToList(),
                Temperature = request.Temperature,
                MaxCompletionTokens = request.MaxTokens
            };

            await foreach (var chunk in _inferenceClient.StreamCompletionAsync(inferRequest, timeoutCts.Token))
            {
                fullResponse.Append(chunk);
                await onChunk(chunk);
            }
        }
        catch (Exception ex)
        {
            primaryFailed = true;
            primaryError = ex.Message;
            _logger.LogError(ex,
                "Primary LLM call failed for request {RequestId} (correlation: {CorrelationId})",
                requestId, _correlationIdAccessor.CorrelationId);
            throw;
        }
        finally
        {
            sw.Stop();

            var status = primaryFailed
                ? RequestStatus.PrimaryLLMResponseFailed
                : RequestStatus.Created;

            var record = new RequestRecord
            {
                RequestId = requestId,
                Status = status,
                Model = primaryModel,
                CandidateModel = _inferenceOptions.CandidateModel,
                RequestPayloadJson = payloadJson
            };

            var primaryResponse = new PrimaryLlmResponse
            {
                RequestId = requestId,
                ResponseText = fullResponse.Length > 0 ? fullResponse.ToString() : null,
                IsError = primaryFailed,
                ErrorMessage = primaryError,
                LatencyMs = sw.ElapsedMilliseconds
            };

            try
            {
                await _requestRepository.AddAsync(record, primaryResponse, CancellationToken.None);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx,
                    "Failed to persist request {RequestId} to database", requestId);
            }

            if (!primaryFailed)
            {
                // Fire-and-forget queue publish — failures must not impact the already-sent response.
                _ = PublishShadowMessageAsync(requestId, payloadJson);
            }
        }
    }

    private async Task PublishShadowMessageAsync(Guid requestId, string payloadJson)
    {
        try
        {
            await _queuePublisher.PublishAsync(new ShadowQueueMessage
            {
                RequestId = requestId,
                RequestPayloadJson = payloadJson,
                CandidateModel = _inferenceOptions.CandidateModel
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish shadow message for request {RequestId} — shadow execution skipped",
                requestId);
        }
    }
}
