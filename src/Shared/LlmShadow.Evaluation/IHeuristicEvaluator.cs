using LlmShadow.Evaluation.Models;

namespace LlmShadow.Evaluation;

/// <summary>
/// Deterministic heuristic that compares a primary LLM response against a candidate LLM response
/// and produces an <see cref="EvaluationResult"/>.
/// Implementations must be stateless and safe for concurrent invocation.
/// </summary>
public interface IHeuristicEvaluator
{
    /// <summary>
    /// Evaluates whether <paramref name="primaryResponse"/> and <paramref name="secondaryResponse"/>
    /// are equivalent according to the configured heuristic.
    /// </summary>
    /// <param name="primaryResponse">The raw text response from the primary LLM.</param>
    /// <param name="secondaryResponse">The raw text response from the candidate LLM.</param>
    /// <returns>An <see cref="EvaluationResult"/> describing the outcome.</returns>
    EvaluationResult Evaluate(string primaryResponse, string secondaryResponse);
}
