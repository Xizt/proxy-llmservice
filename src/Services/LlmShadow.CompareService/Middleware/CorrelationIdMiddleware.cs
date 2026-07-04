using LlmShadow.Common;

namespace LlmShadow.CompareService.Middleware;

/// <summary>Reads or generates a correlation ID and propagates it through the request and response.</summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    /// <summary>Initialises the middleware.</summary>
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
