using FluentAssertions;
using LlmShadow.Evaluation;
using Xunit;

namespace LlmShadow.UnitTests.Evaluation;

/// <summary>Unit tests for <see cref="JsonActionEvaluator"/>.</summary>
public sealed class JsonActionEvaluatorTests
{
    private readonly JsonActionEvaluator _sut = new();

    [Fact]
    public void Evaluate_BothHaveMatchingAction_ReturnsMatchedResult()
    {
        // Arrange
        var primary   = """{"action": "navigate", "target": "/home"}""";
        var secondary = """{"action": "navigate", "url": "/home"}""";

        // Act
        var result = _sut.Evaluate(primary, secondary);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsMatch.Should().BeTrue();
        result.MatchPercentage.Should().Be(100.0);
        result.PrimaryAction.Should().Be("navigate");
        result.SecondaryAction.Should().Be("navigate");
    }

    [Fact]
    public void Evaluate_BothHaveDifferentActions_ReturnsMismatchedResult()
    {
        // Arrange
        var primary   = """{"action": "navigate"}""";
        var secondary = """{"action": "search"}""";

        // Act
        var result = _sut.Evaluate(primary, secondary);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsMatch.Should().BeFalse();
        result.MatchPercentage.Should().Be(0.0);
        result.PrimaryAction.Should().Be("navigate");
        result.SecondaryAction.Should().Be("search");
    }

    [Fact]
    public void Evaluate_PrimaryIsNotValidJson_ReturnsFailure()
    {
        // Arrange
        var primary   = "not json at all";
        var secondary = """{"action": "search"}""";

        // Act
        var result = _sut.Evaluate(primary, secondary);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("Primary");
    }

    [Fact]
    public void Evaluate_SecondaryIsNotValidJson_ReturnsFailure()
    {
        // Arrange
        var primary   = """{"action": "navigate"}""";
        var secondary = "{broken json";

        // Act
        var result = _sut.Evaluate(primary, secondary);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("Secondary");
    }

    [Fact]
    public void Evaluate_PrimaryMissingActionKey_ReturnsFailure()
    {
        // Arrange
        var primary   = """{"intent": "navigate"}""";
        var secondary = """{"action": "navigate"}""";

        // Act
        var result = _sut.Evaluate(primary, secondary);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("action");
    }

    [Fact]
    public void Evaluate_SecondaryMissingActionKey_ReturnsFailure()
    {
        // Arrange
        var primary   = """{"action": "navigate"}""";
        var secondary = """{"result": "ok"}""";

        // Act
        var result = _sut.Evaluate(primary, secondary);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("action");
    }

    [Fact]
    public void Evaluate_ActionIsNotAString_ReturnsFailure()
    {
        // Arrange
        var primary   = """{"action": 42}""";
        var secondary = """{"action": "navigate"}""";

        // Act
        var result = _sut.Evaluate(primary, secondary);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_BothEmptyStrings_ReturnsFailure()
    {
        // Act
        var result = _sut.Evaluate(string.Empty, string.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Evaluate_ActionMatchIsCaseSensitive()
    {
        // Arrange — same word, different case
        var primary   = """{"action": "Navigate"}""";
        var secondary = """{"action": "navigate"}""";

        // Act
        var result = _sut.Evaluate(primary, secondary);

        // Assert — ordinal comparison; "Navigate" != "navigate"
        result.IsSuccess.Should().BeTrue();
        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_JsonArrayRoot_ReturnsFailure()
    {
        // Arrange
        var primary   = """[{"action": "navigate"}]""";
        var secondary = """{"action": "navigate"}""";

        // Act
        var result = _sut.Evaluate(primary, secondary);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }
}
