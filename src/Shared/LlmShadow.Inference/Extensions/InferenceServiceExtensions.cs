using System.Net.Http.Headers;
using LlmShadow.Common.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LlmShadow.Inference.Extensions;

/// <summary>Extension methods for registering the DO Inference HTTP client.</summary>
public static class InferenceServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IInferenceClient"/> as a typed HTTP client backed by
    /// <see cref="DigitalOceanInferenceClient"/>.
    /// Requires <see cref="InferenceOptions"/> to be bound before calling this method.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The updated <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddInferenceClient(this IServiceCollection services)
    {
        services.AddHttpClient<IInferenceClient, DigitalOceanInferenceClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<InferenceOptions>>().Value;
            var baseUrl = opts.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", opts.ModelAccessKey);
            // Allow enough time for streaming responses; individual call timeouts are managed
            // via CancellationToken passed to each method.
            client.Timeout = TimeSpan.FromSeconds(
                Math.Max(opts.PrimaryTimeoutSeconds, opts.CandidateTimeoutSeconds) + 30);
        });

        return services;
    }
}
