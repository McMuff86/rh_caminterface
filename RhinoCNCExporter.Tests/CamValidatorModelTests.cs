using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for CamValidator models: ValidationResult, ValidationIssue, and Severity enum.
/// These tests validate the pure logic of the validation models without RhinoCommon.
/// </summary>
public class CamValidatorTests
{
    #region ValidationResult

    [Fact]
    public void ValidationResult_EmptyResult_IsClean()
    {
        var result = new ValidationResult();
        Assert.True(result.IsClean);
        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Equal(0, result.InfoCount);
    }

    [Fact]
    public void ValidationResult_WithErrors_HasErrorsIsTrue()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue
        {
            Severity = Severity.Error,
            Message = "Test error"
        });

        Assert.True(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.False(result.IsClean);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public void ValidationResult_WithWarnings_HasWarningsIsTrue()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue
        {
            Severity = Severity.Warning,
            Message = "Test warning"
        });

        Assert.False(result.HasErrors);
        Assert.True(result.HasWarnings);
        Assert.False(result.IsClean);
        Assert.Equal(1, result.WarningCount);
    }

    [Fact]
    public void ValidationResult_WithInfoOnly_NoErrorsNoWarnings()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue
        {
            Severity = Severity.Info,
            Message = "Info message"
        });

        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.False(result.IsClean);
        Assert.Equal(1, result.InfoCount);
    }

    [Fact]
    public void ValidationResult_MixedSeverities_CountsCorrectly()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue { Severity = Severity.Error, Message = "Error 1" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Error, Message = "Error 2" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Warning, Message = "Warning 1" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Warning, Message = "Warning 2" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Warning, Message = "Warning 3" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Info, Message = "Info 1" });

        Assert.Equal(2, result.ErrorCount);
        Assert.Equal(3, result.WarningCount);
        Assert.Equal(1, result.InfoCount);
        Assert.True(result.HasErrors);
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void ValidationResult_FormatSummary_NoIssues_ReturnsCleanMessage()
    {
        var result = new ValidationResult();
        Assert.Equal("Keine Probleme gefunden", result.FormatSummary());
    }

    [Fact]
    public void ValidationResult_FormatSummary_ErrorsOnly()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue { Severity = Severity.Error, Message = "E1" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Error, Message = "E2" });

        Assert.Equal("2 Fehler", result.FormatSummary());
    }

    [Fact]
    public void ValidationResult_FormatSummary_WarningsOnly_Singular()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue { Severity = Severity.Warning, Message = "W1" });

        Assert.Equal("1 Warnung", result.FormatSummary());
    }

    [Fact]
    public void ValidationResult_FormatSummary_WarningsOnly_Plural()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue { Severity = Severity.Warning, Message = "W1" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Warning, Message = "W2" });

        Assert.Equal("2 Warnungen", result.FormatSummary());
    }

    [Fact]
    public void ValidationResult_FormatSummary_MixedSeverities()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue { Severity = Severity.Error, Message = "E" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Warning, Message = "W1" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Warning, Message = "W2" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Info, Message = "I" });

        Assert.Equal("1 Fehler, 2 Warnungen, 1 Info", result.FormatSummary());
    }

    [Fact]
    public void ValidationResult_FormatSummary_InfoPlural()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue { Severity = Severity.Info, Message = "I1" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Info, Message = "I2" });

        Assert.Equal("2 Infos", result.FormatSummary());
    }

    #endregion

    #region ValidationIssue

    [Fact]
    public void ValidationIssue_DefaultValues()
    {
        var issue = new ValidationIssue();
        Assert.Equal(Severity.Info, issue.Severity); // default enum value
        Assert.Equal(string.Empty, issue.Message);
        Assert.Null(issue.ObjectId);
        Assert.Equal(string.Empty, issue.Category);
    }

    [Fact]
    public void ValidationIssue_SetProperties()
    {
        var objectId = Guid.NewGuid();
        var issue = new ValidationIssue
        {
            Severity = Severity.Error,
            Message = "Tool not assigned",
            ObjectId = objectId,
            Category = "Werkzeug"
        };

        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal("Tool not assigned", issue.Message);
        Assert.Equal(objectId, issue.ObjectId);
        Assert.Equal("Werkzeug", issue.Category);
    }

    [Fact]
    public void ValidationIssue_NullObjectId_IsAllowed()
    {
        var issue = new ValidationIssue
        {
            Severity = Severity.Warning,
            Message = "General warning",
            ObjectId = null,
            Category = "General"
        };

        Assert.Null(issue.ObjectId);
        Assert.False(issue.ObjectId.HasValue);
    }

    #endregion

    #region Severity Enum

    [Theory]
    [InlineData(Severity.Info, 0)]
    [InlineData(Severity.Warning, 1)]
    [InlineData(Severity.Error, 2)]
    public void Severity_EnumValues_AreCorrect(Severity severity, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)severity);
    }

    [Fact]
    public void Severity_AllValuesExist()
    {
        var values = Enum.GetValues<Severity>();
        Assert.Equal(3, values.Length);
        Assert.Contains(Severity.Info, values);
        Assert.Contains(Severity.Warning, values);
        Assert.Contains(Severity.Error, values);
    }

    #endregion
}
