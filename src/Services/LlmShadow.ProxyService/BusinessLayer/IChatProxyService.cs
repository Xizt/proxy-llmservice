using LlmShadow.Models.Request;

namespace LlmShadow.ProxyService.BusinessLayer;

/// <summary>
/// Core hot-path service that proxies a chat request to the primary LLM and enqueues a shadow message
/// for the candidate model.
/// </summary>
public interface IChatProxyService
{
    /// <summary>
    /// Invokes the primary LLM streaming endpoint. Calls <paramref name="onChunk"/> with each
    /// content delta as it arrives, buffers the full response, persists the request record and
    /// primary response to the database, then publishes a shadow message to the Redis Stream.
    /// </summary>
    /// <param name="request">The validated incoming chat request DTO.</param>
    /// <param name="onChunk">
    /// Callback invoked for every content token delta received from the primary model.
    /// Used by the controller to write SSE data frames to the HTTP response.
    /// </param>
    /// <param name="cancellationToken">Token propagated from the client connection.</param>
    Task ExecuteAsync(
        ChatRequestDto request,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken);
}
