using LlmShadow.Models.Common;

namespace LlmShadow.SecondaryProcessor.BusinessLayer;

/// <summary>Executes a single shadow message: calls the candidate LLM and persists the response.</summary>
public interface IShadowExecutionService
{
    /// <summary>
    /// Processes <paramref name="message"/> by calling the candidate LLM (non-streaming),
    /// persisting the <see cref="LlmShadow.DataLayer.Models.SecondaryLlmResponse"/>, and updating
    /// the parent <see cref="LlmShadow.DataLayer.Models.RequestRecord"/> status on error or timeout.
    /// </summary>
    /// <param name="message">The shadow queue message to process.</param>
    /// <param name="cancellationToken">Token used to abort the operation.</param>
    Task ExecuteAsync(ShadowQueueMessage message, CancellationToken cancellationToken);
}
