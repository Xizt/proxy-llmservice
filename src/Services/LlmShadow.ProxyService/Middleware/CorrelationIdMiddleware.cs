using LlmShadow.Common;

namespace LlmShadow.ProxyService.Middleware;

/// <summary>
/// Reads the <c>X-Correlation-Id</c> request header and sets it on the
/// scoped <see cref="ICorrelationIdAccessor"/>. Generates a new ID when the header is absent.
/// The same ID is echoed back in the response.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    /// <summary>Initialises the middleware with the next pipeline delegate.</summary>
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    /// <inheritdoc cref="IMiddleware.InvokeAsync"/>
    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationIdAccessor)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value)
            ? value.ToString()
            : Guid.NewGuid().ToString("N");

        correlationIdAccessor.SetCorrelationId(correlationId);
        context.Response.Headers[HeaderName] = correlationId;

        await _next(context);
    }
}
