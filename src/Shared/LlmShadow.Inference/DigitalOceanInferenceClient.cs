using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using LlmShadow.Common.Options;
using LlmShadow.Inference.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace LlmShadow.Inference;

/// <summary>
/// OpenAI-compatible HTTP client for the DigitalOcean Serverless Inference API
/// (<c>https://inference.do-ai.run/v1</c>).
/// </summary>
public sealed class DigitalOceanInferenceClient : IInferenceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<DigitalOceanInferenceClient> _logger;
    private readonly InferenceOptions _options;
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;

    /// <summary>Initialises the client using a pre-configured <see cref="HttpClient"/> from <see cref="IHttpClientFactory"/>.</summary>
    public DigitalOceanInferenceClient(
        HttpClient httpClient,
        IOptions<InferenceOptions> options,
        ILogger<DigitalOceanInferenceClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = _options.RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r =>
                        r.StatusCode == HttpStatusCode.TooManyRequests ||
                        (int)r.StatusCode >= 500)
            })
            .Build();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamCompletionAsync(
        InferenceRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var httpRequest = BuildHttpRequest(request, stream: true);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming request to DO Inference failed for model {Model}", request.Model);
            throw;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            var chunk = ParseStreamDelta(data);
            if (chunk is not null)
                yield return chunk;
        }
    }

    /// <inheritdoc />
    public async Task<InferenceResponse> CompleteAsync(
        InferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var resilienceContext = ResilienceContextPool.Shared.Get(cancellationToken);
        try
        {
            var outcome = await _retryPipeline.ExecuteOutcomeAsync(
                async (ctx, state) =>
                {
                    var req = BuildHttpRequest(state, stream: false);
                    try
                    {
                        var resp = await _httpClient.SendAsync(req, ctx.CancellationToken);
                        return Outcome.FromResult(resp);
                    }
                    catch (Exception ex)
                    {
                        return Outcome.FromException<HttpResponseMessage>(ex);
                    }
                },
                resilienceContext,
                request);

            if (outcome.Exception is not null)
            {
                _logger.LogError(outcome.Exception,
                    "Non-streaming request to DO Inference failed for model {Model}", request.Model);
                throw outcome.Exception;
            }

            var httpResponse = outcome.Result!;
            httpResponse.EnsureSuccessStatusCode();

            var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            return ParseResponse(json);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(resilienceContext);
        }
    }

    private HttpRequestMessage BuildHttpRequest(InferenceRequest request, bool stream)
    {
        var body = new
        {
            model = request.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            stream,
            temperature = request.Temperature,
            max_completion_tokens = request.MaxCompletionTokens
        };

        var msg = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };

        return msg;
    }

    private static string? ParseStreamDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0) return null;

            var delta = choices[0].GetProperty("delta");
            if (delta.TryGetProperty("content", out var content))
                return content.GetString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static InferenceResponse ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var choices = root.GetProperty("choices");
        var content = choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        var model = root.TryGetProperty("model", out var m) ? (m.GetString() ?? string.Empty) : string.Empty;
        return new InferenceResponse { Content = content, Model = model };
    }
}
