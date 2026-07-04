using LlmShadow.DataLayer.Models;

namespace LlmShadow.DataLayer.Repositories;

/// <summary>Data-access contract for storing secondary (shadow) LLM responses.</summary>
public interface ISecondaryResponseRepository
{
    /// <summary>Persists a new <see cref="SecondaryLlmResponse"/> row.</summary>
    Task AddAsync(SecondaryLlmResponse response, CancellationToken cancellationToken = default);
}
