using System.Text.Json;
using LlmShadow.Evaluation.Models;

namespace LlmShadow.Evaluation;

/// <summary>
/// Deterministic heuristic evaluator that:
/// <list type="number">
///   <item>Parses both responses as JSON objects.</item>
///   <item>Extracts the top-level <c>action</c> string property from each.</item>
///   <item>Performs an ordinal exact-match comparison of the two values.</item>
/// </list>
/// Returns <see cref="EvaluationResult.Failure"/> when either response is not valid JSON
/// or does not contain the <c>action</c> key.
/// </summary>
public sealed class JsonActionEvaluator : IHeuristicEvaluator
{
    /// <inheritdoc />
    public EvaluationResult Evaluate(string primaryResponse, string secondaryResponse)
    {
        if (string.IsNullOrWhiteSpace(primaryResponse))
            return EvaluationResult.Failure("Primary response is empty.");

        if (string.IsNullOrWhiteSpace(secondaryResponse))
            return EvaluationResult.Failure("Secondary response is empty.");

        var primaryAction = ExtractAction(primaryResponse, out var primaryError);
        if (primaryAction is null)
            return EvaluationResult.Failure($"Primary response parse error: {primaryError}");

        var secondaryAction = ExtractAction(secondaryResponse, out var secondaryError);
        if (secondaryAction is null)
            return EvaluationResult.Failure($"Secondary response parse error: {secondaryError}");

        var isMatch = string.Equals(primaryAction, secondaryAction, StringComparison.Ordinal);

        return new EvaluationResult
        {
            IsMatch = isMatch,
            MatchPercentage = isMatch ? 100.0 : 0.0,
            PrimaryAction = primaryAction,
            SecondaryAction = secondaryAction
        };
    }

    /// <summary>
    /// Attempts to parse <paramref name="responseText"/> as a JSON object and extract
    /// the <c>action</c> string property.
    /// </summary>
    /// <param name="responseText">Raw text to parse.</param>
    /// <param name="error">Populated with a description when extraction fails; <c>null</c> on success.</param>
    /// <returns>The action string value, or <c>null</c> when extraction fails.</returns>
    private static string? ExtractAction(string responseText, out string? error)
    {
        error = null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(responseText);
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return null;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = $"Expected a JSON object but got {doc.RootElement.ValueKind}.";
                return null;
            }

            if (!doc.RootElement.TryGetProperty("action", out var actionProp))
            {
                error = "Property 'action' not found in the JSON object.";
                return null;
            }

            if (actionProp.ValueKind != JsonValueKind.String)
            {
                error = $"Property 'action' is not a string (found {actionProp.ValueKind}).";
                return null;
            }

            return actionProp.GetString();
        }
    }
}
