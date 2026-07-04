using FluentAssertions;
using LlmShadow.Evaluation;
using LlmShadow.Evaluation.Models;
using Xunit;

namespace LlmShadow.UnitTests.Inference;

/// <summary>
/// Tests for parsing inference response text through <see cref="JsonActionEvaluator"/>,
/// covering the typical shapes returned by real LLM APIs.
/// </summary>
public sealed class InferenceResponseParsingTests
{
    private readonly JsonActionEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_MinimalValidJson_ExtractsAction()
    {
        // Arrange — minimal valid payload
        var response = """{"action":"submit"}""";

        // Act
        var result = _evaluator.Evaluate(response, response);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsMatch.Should().BeTrue();
        result.PrimaryAction.Should().Be("submit");
    }

    [Fact]
    public void Evaluate_ResponseWithNestedObjects_OnlyTopLevelActionMatters()
    {
        // Arrange — action is at top level; nested 'action' should not interfere
        var primary   = """{"action": "search", "params": {"action": "ignored"}}""";
        var secondary = """{"action": "search"}""";

        // Act
        var result = _evaluator.Evaluate(primary, secondary);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ResponseWithWhitespaceContent_StillFails()
    {
        // Act
        var result = _evaluator.Evaluate("   ", """{"action":"ok"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ActionValueIsNull_ReturnsFailure()
    {
        // Arrange
        var primary   = """{"action": null}""";
        var secondary = """{"action": "navigate"}""";

        // Act
        var result = _evaluator.Evaluate(primary, secondary);

        // Assert — null is not a string
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData("click", "click", true)]
    [InlineData("click", "CLICK", false)]
    [InlineData("submit_form", "submit_form", true)]
    [InlineData("navigate", "navigate_back", false)]
    public void Evaluate_VariousActionPairs_ReturnsExpectedMatch(
        string primaryAction, string secondaryAction, bool expected)
    {
        // Arrange
        var primary   = $$"""{"action": "{{primaryAction}}"}""";
        var secondary = $$"""{"action": "{{secondaryAction}}"}""";

        // Act
        var result = _evaluator.Evaluate(primary, secondary);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsMatch.Should().Be(expected);
    }

    [Fact]
    public void EvaluationResult_Failure_HasCorrectDefaults()
    {
        // Act
        var result = EvaluationResult.Failure("test reason");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsMatch.Should().BeFalse();
        result.MatchPercentage.Should().Be(0.0);
        result.FailureReason.Should().Be("test reason");
    }
}
