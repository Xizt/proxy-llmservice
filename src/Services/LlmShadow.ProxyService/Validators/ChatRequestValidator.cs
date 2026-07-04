using FluentValidation;
using LlmShadow.Models.Request;

namespace LlmShadow.ProxyService.Validators;

/// <summary>FluentValidation validator for <see cref="ChatRequestDto"/>.</summary>
public sealed class ChatRequestValidator : AbstractValidator<ChatRequestDto>
{
    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "system", "user", "assistant" };

    /// <summary>Defines all validation rules for <see cref="ChatRequestDto"/>.</summary>
    public ChatRequestValidator()
    {
        RuleFor(x => x.Messages)
            .NotEmpty()
            .WithMessage("At least one message is required.");

        RuleForEach(x => x.Messages).ChildRules(msg =>
        {
            msg.RuleFor(m => m.Role)
                .NotEmpty()
                .Must(r => AllowedRoles.Contains(r))
                .WithMessage("Role must be one of: system, user, assistant.");

            msg.RuleFor(m => m.Content)
                .NotEmpty()
                .WithMessage("Message content must not be empty.");
        });

        When(x => x.Temperature.HasValue, () =>
        {
            RuleFor(x => x.Temperature!.Value)
                .InclusiveBetween(0.0, 2.0)
                .WithMessage("Temperature must be between 0 and 2.");
        });

        When(x => x.MaxTokens.HasValue, () =>
        {
            RuleFor(x => x.MaxTokens!.Value)
                .GreaterThan(0)
                .WithMessage("MaxTokens must be greater than 0.");
        });
    }
}
