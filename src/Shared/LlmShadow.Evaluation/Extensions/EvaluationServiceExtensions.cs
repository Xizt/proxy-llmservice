using Microsoft.Extensions.DependencyInjection;

namespace LlmShadow.Evaluation.Extensions;

/// <summary>Extension methods for registering the evaluation library.</summary>
public static class EvaluationServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IHeuristicEvaluator"/> as a singleton <see cref="JsonActionEvaluator"/>.
    /// The evaluator is stateless and safe for concurrent use.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The updated <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddEvaluation(this IServiceCollection services)
    {
        services.AddSingleton<IHeuristicEvaluator, JsonActionEvaluator>();
        return services;
    }
}
