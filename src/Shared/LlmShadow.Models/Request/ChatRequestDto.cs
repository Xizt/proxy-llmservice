using System.ComponentModel.DataAnnotations;

namespace LlmShadow.Models.Request;

/// <summary>Inbound DTO for the <c>POST /v1/chat</c> endpoint.</summary>
public sealed record ChatRequestDto
{
    /// <summary>Gets the ordered list of chat messages forming the conversation context.</summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one message is required.")]
    public IReadOnlyList<ChatMessageDto> Messages { get; init; } = Array.Empty<ChatMessageDto>();

    /// <summary>
    /// Gets an optional model override. When omitted the proxy uses the configured primary model.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>Gets an optional sampling temperature (0–2). Passed through to the LLM API as-is.</summary>
    [Range(0.0, 2.0, ErrorMessage = "Temperature must be between 0 and 2.")]
    public double? Temperature { get; init; }

    /// <summary>Gets an optional cap on completion tokens.</summary>
    [Range(1, 32_768, ErrorMessage = "MaxTokens must be between 1 and 32 768.")]
    public int? MaxTokens { get; init; }
}

/// <summary>A single role-attributed message within a chat conversation.</summary>
public sealed record ChatMessageDto
{
    /// <summary>Gets the role of the message author (<c>system</c>, <c>user</c>, or <c>assistant</c>).</summary>
    [Required]
    public string Role { get; init; } = string.Empty;

    /// <summary>Gets the text content of the message.</summary>
    [Required]
    public string Content { get; init; } = string.Empty;
}
