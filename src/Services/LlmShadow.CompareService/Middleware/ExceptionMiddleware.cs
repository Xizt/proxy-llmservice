using System.Net;
using LlmShadow.Common;
using LlmShadow.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace LlmShadow.CompareService.Middleware;

/// <summary>Centralized exception handler returning RFC 7807 Problem Details.</summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    /// <summary>Initialises the middleware.</summary>
    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <inheritdoc cref="IMiddleware.InvokeAsync"/>
    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationIdAccessor)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(context, HttpStatusCode.NotFound, ex.Message, ex.ErrorCode, correlationIdAccessor.CorrelationId);
        }
        catch (DomainException ex) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, ex.Message, ex.ErrorCode, correlationIdAccessor.CorrelationId);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — no response needed.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path} (correlation: {CorrelationId})",
                context.Request.Method, context.Request.Path, correlationIdAccessor.CorrelationId);

            if (!context.Response.HasStarted)
            {
                await WriteProblemAsync(context, HttpStatusCode.InternalServerError,
                    "An unexpected error occurred.", "INTERNAL_ERROR", correlationIdAccessor.CorrelationId);
            }
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        HttpStatusCode status,
        string detail,
        string errorCode,
        string correlationId)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = status == HttpStatusCode.NotFound ? "Not Found" : "Error",
            Detail = detail,
            Instance = context.Request.Path,
            Extensions = { ["errorCode"] = errorCode, ["correlationId"] = correlationId }
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}
