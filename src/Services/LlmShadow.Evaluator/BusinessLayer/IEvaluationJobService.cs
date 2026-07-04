namespace LlmShadow.Evaluator.BusinessLayer;

/// <summary>Runs a single batch of heuristic evaluations over unevaluated request records.</summary>
public interface IEvaluationJobService
{
    /// <summary>
    /// Fetches up to <paramref name="batchSize"/> <c>Created</c> requests that have both a primary
    /// and a secondary response, evaluates each with <see cref="LlmShadow.Evaluation.IHeuristicEvaluator"/>,
    /// and persists the evaluation outcome back to the database.
    /// </summary>
    /// <param name="batchSize">Maximum number of records to evaluate in this run.</param>
    /// <param name="cancellationToken">Token used to abort processing.</param>
    Task RunBatchAsync(int batchSize, CancellationToken cancellationToken);
}
