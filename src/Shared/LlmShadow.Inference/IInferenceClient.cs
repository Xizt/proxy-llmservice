using LlmShadow.Inference.Models;

namespace LlmShadow.Inference;

/// <summary>
/// Abstraction over the DigitalOcean Serverless Inference API.
/// Provides both a streaming and a non-streaming completion path.
/// </summary>
public interface IInferenceClient
{
    /// <summary>
    /// Streams token deltas from the primary LLM as an async sequence of content strings.
    /// Each yielded string is one or more characters from the <c>delta.content</c> field of the
    /// OpenAI-compatible SSE stream.
    /// </summary>
    /// <param name="request">The inference parameters including model and messages.</param>
    /// <param name="cancellationToken">Token used to abort the streaming request.</param>
    /// <returns>An async sequence of content delta strings.</returns>
    IAsyncEnumerable<string> StreamCompletionAsync(
        InferenceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls the LLM non-streaming and returns the full completion once available.
    /// Used by the SecondaryProcessor for shadow execution.
    /// </summary>
    /// <param name="request">The inference parameters including model and messages.</param>
    /// <param name="cancellationToken">Token used to abort the request.</param>
    /// <returns>The complete <see cref="InferenceResponse"/>.</returns>
    Task<InferenceResponse> CompleteAsync(
        InferenceRequest request,
        CancellationToken cancellationToken = default);
}
