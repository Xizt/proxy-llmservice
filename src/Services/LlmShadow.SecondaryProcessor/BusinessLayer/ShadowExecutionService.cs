using System.Diagnostics;
using System.Text.Json;
using LlmShadow.Common.Options;
using LlmShadow.DataLayer.Models;
using LlmShadow.DataLayer.Repositories;
using LlmShadow.Inference;
using LlmShadow.Inference.Models;
using LlmShadow.Models.Common;
using LlmShadow.Models.Request;
using Microsoft.Extensions.Options;

namespace LlmShadow.SecondaryProcessor.BusinessLayer;

/// <summary>Implementation of <see cref="IShadowExecutionService"/>.</summary>
public sealed class ShadowExecutionService : IShadowExecutionService
{
    private readonly IInferenceClient _inferenceClient;
    private readonly ISecondaryResponseRepository _secondaryRepo;
    private readonly IRequestRepository _requestRepo;
    private readonly InferenceOptions _inferenceOptions;
    private readonly ILogger<ShadowExecutionService> _logger;

    /// <summary>Initialises the service with all required dependencies.</summary>
    public ShadowExecutionService(
        IInferenceClient inferenceClient,
        ISecondaryResponseRepository secondaryRepo,
        IRequestRepository requestRepo,
        IOptions<InferenceOptions> inferenceOptions,
        ILogger<ShadowExecutionService> logger)
    {
        _inferenceClient = inferenceClient;
        _secondaryRepo = secondaryRepo;
        _requestRepo = requestRepo;
        _inferenceOptions = inferenceOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(ShadowQueueMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing shadow message for request {RequestId} using model {Model}",
            message.RequestId, message.CandidateModel);

        var sw = Stopwatch.StartNew();
        var responseText = default(string?);
        var isError = false;
        var errorMessage = default(string?);
        var status = RequestStatus.Created;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_inferenceOptions.CandidateTimeoutSeconds));

        try
        {
            var inferRequest = BuildInferenceRequest(message);
            var response = await _inferenceClient.CompleteAsync(inferRequest, timeoutCts.Token);
            responseText = response.Content;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            isError = true;
            errorMessage = $"Candidate LLM call timed out after {_inferenceOptions.CandidateTimeoutSeconds}s.";
            status = RequestStatus.Timedout;

            _logger.LogWarning(
                "Shadow execution timed out for request {RequestId}", message.RequestId);
        }
        catch (Exception ex)
        {
            isError = true;
            errorMessage = ex.Message;
            status = RequestStatus.SecondaryLLMResponseFailed;

            _logger.LogError(ex,
                "Shadow execution failed for request {RequestId}", message.RequestId);
        }
        finally
        {
            sw.Stop();
        }

        var secondaryResponse = new SecondaryLlmResponse
        {
            RequestId = message.RequestId,
            ResponseText = responseText,
            IsError = isError,
            ErrorMessage = errorMessage,
            LatencyMs = sw.ElapsedMilliseconds
        };

        await _secondaryRepo.AddAsync(secondaryResponse, CancellationToken.None);

        if (isError)
        {
            await _requestRepo.UpdateStatusAsync(message.RequestId, status, CancellationToken.None);
        }
    }

    private InferenceRequest BuildInferenceRequest(ShadowQueueMessage message)
    {
        ChatRequestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ChatRequestDto>(message.RequestPayloadJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Could not deserialize request payload for {message.RequestId}: {ex.Message}", ex);
        }

        if (dto is null)
            throw new InvalidOperationException($"Request payload for {message.RequestId} deserialized to null.");

        return new InferenceRequest
        {
            Model = message.CandidateModel,
            Messages = dto.Messages
                .Select(m => new InferenceChatMessage { Role = m.Role, Content = m.Content })
                .ToList(),
            Temperature = dto.Temperature,
            MaxCompletionTokens = dto.MaxTokens
        };
    }
}
