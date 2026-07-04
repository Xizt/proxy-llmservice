using System.Text.Json;
using LlmShadow.Models.Request;
using LlmShadow.ProxyService.BusinessLayer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LlmShadow.ProxyService.Controllers;

/// <summary>Handles the primary hot-path chat proxy endpoint.</summary>
[ApiController]
[Route("v1")]
[EnableRateLimiting("fixed")]
public sealed class ChatController : ControllerBase
{
    private readonly IChatProxyService _chatProxyService;
    private readonly ILogger<ChatController> _logger;

    /// <summary>Initialises the controller.</summary>
    public ChatController(IChatProxyService chatProxyService, ILogger<ChatController> logger)
    {
        _chatProxyService = chatProxyService;
        _logger = logger;
    }

    /// <summary>
    /// Proxies the chat request to the primary LLM and streams the response back to the caller
    /// using Server-Sent Events (SSE). Concurrently enqueues the same request for shadow
    /// execution against the candidate model — without affecting response latency.
    /// </summary>
    /// <param name="request">The chat request payload.</param>
    /// <param name="cancellationToken">Token propagated from the client connection.</param>
    [HttpPost("chat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task Chat([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        await _chatProxyService.ExecuteAsync(
            request,
            async chunk =>
            {
                var frame = JsonSerializer.Serialize(new { content = chunk });
                await Response.WriteAsync($"data: {frame}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            },
            cancellationToken);

        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
