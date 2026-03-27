using System.Globalization;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Additional tests for interactive CAM Core classes.
/// Covers edge cases in CncOperationSchema, MachiningOperation,
/// OperationDefaultsBase, ValidationResult, and OperationStatistics.
/// </summary>
public class AdditionalCoreTests
{
    #region CncOperationSchema.ValidateOperation — Edge Cases

    [Fact]
    public void ValidateOperation_NullType_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, string>();
        var (isValid, error) = CncOperationSchema.ValidateOperation(null!, parameters);

        Assert.False(isValid);
        Assert.Contains("Unknown operation type", error!);
    }

    [Fact]
    public void ValidateOperation_EmptyStringType_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, string>();
        var (isValid, error) = CncOperationSchema.ValidateOperation("", parameters);

        Assert.False(isValid);
        Assert.Contains("Unknown operation type", error!);
    }

    [Fact]
    public void ValidateOperation_ContourMissingDepth_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Fräser 8mm"
        };

        var (isValid, error) = CncOperationSchema.ValidateOperation("Contour", parameters);

        Assert.False(isValid);
        Assert.Equal("Contour operation requires CNC_Depth", error);
    }

    [Fact]
    public void ValidateOperation_PocketMissingTool_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DEPTH] = "5.0"
        };

        var (isValid, error) = CncOperationSchema.ValidateOperation("Pocket", parameters);

        Assert.False(isValid);
        Assert.Equal("Pocket operation requires CNC_Tool", error);
    }

    [Fact]
    public void ValidateOperation_PocketMissingDepth_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Fräser 6mm"
        };

        var (isValid, error) = CncOperationSchema.ValidateOperation("Pocket", parameters);

        Assert.False(isValid);
        Assert.Equal("Pocket operation requires CNC_Depth", error);
    }

    [Fact]
    public void ValidateOperation_DrillMissingDepth_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DIAMETER] = "5.0"
        };

        var (isValid, error) = CncOperationSchema.ValidateOperation("Drill", parameters);

        Assert.False(isValid);
        Assert.Equal("Drill operation requires CNC_Depth", error);
    }

    [Fact]
    public void ValidateOperation_GrooveMissingTool_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_WIDTH] = "5.0",
            [CncOperationSchema.CNC_DEPTH] = "8.0"
        };

        var (isValid, error) = CncOperationSchema.ValidateOperation("Groove", parameters);

        Assert.False(isValid);
        Assert.Equal("Groove operation requires CNC_Tool", error);
    }

    [Fact]
    public void ValidateOperation_GrooveMissingWidth_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Nutfräser 5mm",
            [CncOperationSchema.CNC_DEPTH] = "8.0"
        };

        var (isValid, error) = CncOperationSchema.ValidateOperation("Groove", parameters);

        Assert.False(isValid);
        Assert.Equal("Groove operation requires CNC_Width", error);
    }

    [Fact]
    public void ValidateOperation_GrooveMissingDepth_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Nutfräser 5mm",
            [CncOperationSchema.CNC_WIDTH] = "5.0"
        };

        var (isValid, error) = CncOperationSchema.ValidateOperation("Groove", parameters);

        Assert.False(isValid);
        Assert.Equal("Groove operation requires CNC_Depth", error);
    }

    [Fact]
    public void ValidateOperation_ExtraParametersIgnored_StillValid()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Fräser 8mm",
            [CncOperationSchema.CNC_DEPTH] = "10.0",
            [CncOperationSchema.CNC_FEEDRATE] = "3000",
            [CncOperationSchema.CNC_STRATEGY] = "Finish",
            ["SomeUnknownKey"] = "whatever"
        };

        var (isValid, error) = CncOperationSchema.ValidateOperation("Contour", parameters);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateOperation_EmptyParameterValues_StillPresent()
    {
        // Keys exist but with empty values — still counts as "present"
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "",
            [CncOperationSchema.CNC_DEPTH] = ""
        };

        var (isValid, _) = CncOperationSchema.ValidateOperation("Contour", parameters);
        Assert.True(isValid); // Schema only checks key existence, not value validity
    }

    #endregion

    #region MachiningOperation — Edge Cases

    [Fact]
    public void MachiningOperation_EmptyParameters_AllNull()
    {
        var op = new MachiningOperation("Contour", new Dictionary<string, string>());

        Assert.Null(op.Tool);
        Assert.Null(op.Depth);
        Assert.Null(op.Diameter);
        Assert.Null(op.Width);
        Assert.Null(op.Strategy);
        Assert.Null(op.Feedrate);
        Assert.Null(op.Stepover);
        Assert.Null(op.Peck);
        Assert.Null(op.PeckDepth);
        Assert.Null(op.RampEntry);
    }

    [Fact]
    public void MachiningOperation_WhitespaceValues_ReturnedAsTool()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "   "
        };

        var op = new MachiningOperation("Contour", parameters);
        Assert.Equal("   ", op.Tool); // GetValueOrDefault returns the whitespace string
    }

    [Fact]
    public void MachiningOperation_InvariantCulture_ParsesDecimalsCorrectly()
    {
        // Verify that "10.5" parses with dot as decimal separator (InvariantCulture)
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DEPTH] = "10.5",
            [CncOperationSchema.CNC_FEEDRATE] = "3000.123"
        };

        var op = new MachiningOperation("Contour", parameters);
        Assert.Equal(10.5, op.Depth);
        Assert.Equal(3000.123, op.Feedrate);
    }

    [Fact]
    public void MachiningOperation_CommaDecimalSeparator_ReturnsNull()
    {
        // European comma-separated values should NOT parse (InvariantCulture uses dot)
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DEPTH] = "10,5"
        };

        var op = new MachiningOperation("Contour", parameters);
        Assert.Null(op.Depth); // comma is not valid in InvariantCulture
    }

    [Fact]
    public void MachiningOperation_NegativeValues_ParseCorrectly()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DEPTH] = "-5.0",
            [CncOperationSchema.CNC_FEEDRATE] = "-1000"
        };

        var op = new MachiningOperation("Drill", parameters);
        Assert.Equal(-5.0, op.Depth);
        Assert.Equal(-1000.0, op.Feedrate);
    }

    [Fact]
    public void MachiningOperation_ZeroValues_ParseCorrectly()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DEPTH] = "0",
            [CncOperationSchema.CNC_FEEDRATE] = "0.0"
        };

        var op = new MachiningOperation("Contour", parameters);
        Assert.Equal(0.0, op.Depth);
        Assert.Equal(0.0, op.Feedrate);
    }

    [Fact]
    public void MachiningOperation_VeryLargeNumbers_ParseCorrectly()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_FEEDRATE] = "99999.999"
        };

        var op = new MachiningOperation("Contour", parameters);
        Assert.Equal(99999.999, op.Feedrate);
    }

    [Fact]
    public void MachiningOperation_RecordEquality()
    {
        var params1 = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Fräser 8mm",
            [CncOperationSchema.CNC_DEPTH] = "10.0"
        };

        var op1 = new MachiningOperation("Contour", params1);
        var op2 = new MachiningOperation("Contour", params1);

        // Same reference for parameters → equal
        Assert.Equal(op1, op2);
    }

    [Fact]
    public void MachiningOperation_PeckBoolParsing_EmptyString_ReturnsNull()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_PECK] = ""
        };

        var op = new MachiningOperation("Drill", parameters);
        Assert.Null(op.Peck);
    }

    [Fact]
    public void MachiningOperation_AllParametersSet_AllParsed()
    {
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Test Tool",
            [CncOperationSchema.CNC_DEPTH] = "19.0",
            [CncOperationSchema.CNC_DIAMETER] = "8.0",
            [CncOperationSchema.CNC_WIDTH] = "5.0",
            [CncOperationSchema.CNC_STRATEGY] = "Both",
            [CncOperationSchema.CNC_FEEDRATE] = "3000.0",
            [CncOperationSchema.CNC_STEPOVER] = "45.0",
            [CncOperationSchema.CNC_PECK] = "true",
            [CncOperationSchema.CNC_PECK_DEPTH] = "3.5",
            [CncOperationSchema.CNC_RAMP_ENTRY] = "Spiral"
        };

        var op = new MachiningOperation("Pocket", parameters);

        Assert.Equal("Test Tool", op.Tool);
        Assert.Equal(19.0, op.Depth);
        Assert.Equal(8.0, op.Diameter);
        Assert.Equal(5.0, op.Width);
        Assert.Equal("Both", op.Strategy);
        Assert.Equal(3000.0, op.Feedrate);
        Assert.Equal(45.0, op.Stepover);
        Assert.True(op.Peck);
        Assert.Equal(3.5, op.PeckDepth);
        Assert.Equal("Spiral", op.RampEntry);
    }

    #endregion

    #region OperationDefaultsBase — Additional Edge Cases

    [Fact]
    public void GetDefaults_EmptyMachineKey_DefaultsToNonScm()
    {
        // Empty string shouldn't match "xilog" or "maestrocadt", so it's non-SCM (Biesse path)
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Contour", "");
        Assert.Equal(18.0, defaults.Depth); // Biesse default
        Assert.Equal(4000.0, defaults.Feedrate);
    }

    [Fact]
    public void GetDefaults_UnknownMachineKey_DefaultsToNonScm()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Contour", "someUnknownMachine");
        Assert.Equal(18.0, defaults.Depth); // Biesse default path
    }

    [Fact]
    public void GetDefaults_AllTypes_ReturnNonNullStrategy()
    {
        foreach (var type in OperationDefaultsBase.AllOperationTypes)
        {
            var defaults = OperationDefaultsBase.GetMachineProfileDefaults(type, "xilog");
            Assert.NotNull(defaults.Strategy);
            Assert.NotEmpty(defaults.Strategy);
        }
    }

    [Fact]
    public void GetDefaults_PocketDefaults_HaveValidRampEntry()
    {
        var xilog = OperationDefaultsBase.GetMachineProfileDefaults("Pocket", "xilog");
        var biesse = OperationDefaultsBase.GetMachineProfileDefaults("Pocket", "biesse");

        // Should be one of the known ramp entry types
        var validRampEntries = new[] {
            CncOperationSchema.RAMP_STRAIGHT,
            CncOperationSchema.RAMP_SPIRAL,
            CncOperationSchema.RAMP_PROFILE
        };

        Assert.Contains(xilog.RampEntry, validRampEntries);
        Assert.Contains(biesse.RampEntry, validRampEntries);
    }

    [Fact]
    public void GetDefaults_DrillDefaults_PeckDepthLessThanDepth()
    {
        foreach (var key in new[] { "xilog", "biesse" })
        {
            var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Drill", key);
            Assert.True(defaults.PeckDepth <= defaults.Depth,
                $"Peck depth ({defaults.PeckDepth}) should be <= depth ({defaults.Depth}) for {key}");
        }
    }

    [Fact]
    public void GetDefaults_GrooveDefaults_HavePositiveWidth()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Groove", "xilog");
        Assert.True(defaults.Width > 0, "Groove should have positive default width");
    }

    [Fact]
    public void AllOperationTypes_ImmutableList()
    {
        var types1 = OperationDefaultsBase.AllOperationTypes;
        var types2 = OperationDefaultsBase.AllOperationTypes;

        // Should return equivalent contents
        Assert.Equal(types1.Count, types2.Count);
        for (int i = 0; i < types1.Count; i++)
            Assert.Equal(types1[i], types2[i]);
    }

    #endregion

    #region ValidationResult — Additional Edge Cases

    [Fact]
    public void ValidationResult_LargeNumberOfIssues_CountsCorrectly()
    {
        var result = new ValidationResult();
        for (int i = 0; i < 100; i++)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = i % 3 == 0 ? Severity.Error : i % 3 == 1 ? Severity.Warning : Severity.Info,
                Message = $"Issue {i}"
            });
        }

        Assert.Equal(34, result.ErrorCount);   // 0,3,6,...,99 → 34
        Assert.Equal(33, result.WarningCount);  // 1,4,7,...,97 → 33
        Assert.Equal(33, result.InfoCount);     // 2,5,8,...,98 → 33
        Assert.True(result.HasErrors);
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void ValidationIssue_CategoryDefault_IsEmptyString()
    {
        var issue = new ValidationIssue();
        Assert.Equal(string.Empty, issue.Category);
    }

    [Fact]
    public void ValidationResult_FormatSummary_AllThreeTypes()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue { Severity = Severity.Error, Message = "E" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Warning, Message = "W" });
        result.Issues.Add(new ValidationIssue { Severity = Severity.Info, Message = "I" });

        var summary = result.FormatSummary();
        Assert.Equal("1 Fehler, 1 Warnung, 1 Info", summary);
    }

    #endregion

    #region OperationStatistics — Additional Edge Cases

    [Fact]
    public void OperationStatistics_FormatSummary_OnlyDrills()
    {
        var stats = new OperationStatistics
        {
            TotalOperations = 5,
            DrillCount = 5,
            MaxDepth = 13.0,
            EstimatedTimeMinutes = 0.5
        };

        var summary = stats.FormatSummary();
        Assert.Contains("5× Drill", summary);
        Assert.DoesNotContain("Contour", summary);
        Assert.DoesNotContain("Pocket", summary);
        Assert.DoesNotContain("Groove", summary);
    }

    [Fact]
    public void OperationStatistics_FormatSummary_VeryLongTime()
    {
        var stats = new OperationStatistics
        {
            TotalOperations = 50,
            ContourCount = 50,
            MaxDepth = 25.0,
            EstimatedTimeMinutes = 120.5
        };

        var summary = stats.FormatSummary();
        Assert.Contains("~120.5 min", summary);
    }

    [Fact]
    public void OperationStatistics_FormatSummary_VeryShortTime_LessThanSecond()
    {
        var stats = new OperationStatistics
        {
            TotalOperations = 1,
            DrillCount = 1,
            EstimatedTimeMinutes = 0.01 // ~0.6 seconds
        };

        var summary = stats.FormatSummary();
        Assert.Contains("~1 sec", summary); // 0.01 * 60 = 0.6, formatted as "~1 sec"
    }

    #endregion

    #region CncOperationSchema — Constant Integrity

    [Fact]
    public void EdgeExtractionKeys_AreProperlySuffixed()
    {
        // Verify edge extraction keys follow the same CNC_ convention
        Assert.StartsWith("CNC_", CncOperationSchema.CNC_SOURCE_BREP);
        Assert.StartsWith("CNC_", CncOperationSchema.CNC_SOURCE_EDGE_INDEX);
    }

    [Fact]
    public void AllStrategyConstants_AreUnique()
    {
        var strategies = new[]
        {
            CncOperationSchema.STRATEGY_ROUGH,
            CncOperationSchema.STRATEGY_FINISH,
            CncOperationSchema.STRATEGY_BOTH
        };

        Assert.Equal(strategies.Length, strategies.Distinct().Count());
    }

    [Fact]
    public void AllRampEntryConstants_AreUnique()
    {
        var rampEntries = new[]
        {
            CncOperationSchema.RAMP_STRAIGHT,
            CncOperationSchema.RAMP_SPIRAL,
            CncOperationSchema.RAMP_PROFILE
        };

        Assert.Equal(rampEntries.Length, rampEntries.Distinct().Count());
    }

    [Fact]
    public void AllOperationTypeConstants_AreUnique()
    {
        var types = new[]
        {
            CncOperationSchema.TYPE_CONTOUR,
            CncOperationSchema.TYPE_POCKET,
            CncOperationSchema.TYPE_DRILL,
            CncOperationSchema.TYPE_GROOVE
        };

        Assert.Equal(types.Length, types.Distinct().Count());
    }

    #endregion

    #region Cross-Model Integration Tests

    [Fact]
    public void DefaultsMatchSchemaConstants_Strategies()
    {
        // Verify that defaults use valid strategy constants
        var validStrategies = new HashSet<string>
        {
            CncOperationSchema.STRATEGY_ROUGH,
            CncOperationSchema.STRATEGY_FINISH,
            CncOperationSchema.STRATEGY_BOTH
        };

        foreach (var type in OperationDefaultsBase.AllOperationTypes)
        {
            var defaults = OperationDefaultsBase.GetMachineProfileDefaults(type, "xilog");
            Assert.Contains(defaults.Strategy, validStrategies);
        }
    }

    [Fact]
    public void DefaultsMatchSchemaConstants_RampEntries()
    {
        var validRampEntries = new HashSet<string>
        {
            CncOperationSchema.RAMP_STRAIGHT,
            CncOperationSchema.RAMP_SPIRAL,
            CncOperationSchema.RAMP_PROFILE
        };

        foreach (var type in OperationDefaultsBase.AllOperationTypes)
        {
            var defaults = OperationDefaultsBase.GetMachineProfileDefaults(type, "xilog");
            Assert.Contains(defaults.RampEntry, validRampEntries);
        }
    }

    [Fact]
    public void ValidateOperation_WithDefaultValues_IsAlwaysValid()
    {
        // Operations created with default values should always pass validation
        var machineKeys = new[] { "xilog", "biesse" };
        var typeToValidation = new Dictionary<string, Func<OperationDefaultValues, Dictionary<string, string>>>
        {
            ["Contour"] = d => new Dictionary<string, string>
            {
                [CncOperationSchema.CNC_TOOL] = d.ToolName ?? "DefaultTool",
                [CncOperationSchema.CNC_DEPTH] = d.Depth.ToString("F3", CultureInfo.InvariantCulture)
            },
            ["Pocket"] = d => new Dictionary<string, string>
            {
                [CncOperationSchema.CNC_TOOL] = d.ToolName ?? "DefaultTool",
                [CncOperationSchema.CNC_DEPTH] = d.Depth.ToString("F3", CultureInfo.InvariantCulture)
            },
            ["Drill"] = d => new Dictionary<string, string>
            {
                [CncOperationSchema.CNC_DIAMETER] = d.Diameter.ToString("F3", CultureInfo.InvariantCulture),
                [CncOperationSchema.CNC_DEPTH] = d.Depth.ToString("F3", CultureInfo.InvariantCulture)
            },
            ["Groove"] = d => new Dictionary<string, string>
            {
                [CncOperationSchema.CNC_TOOL] = d.ToolName ?? "DefaultTool",
                [CncOperationSchema.CNC_WIDTH] = d.Width.ToString("F3", CultureInfo.InvariantCulture),
                [CncOperationSchema.CNC_DEPTH] = d.Depth.ToString("F3", CultureInfo.InvariantCulture)
            }
        };

        foreach (var machineKey in machineKeys)
        {
            foreach (var (type, buildParams) in typeToValidation)
            {
                var defaults = OperationDefaultsBase.GetMachineProfileDefaults(type, machineKey);
                var parameters = buildParams(defaults);
                var (isValid, error) = CncOperationSchema.ValidateOperation(type, parameters);

                Assert.True(isValid, $"{type} with {machineKey} defaults should be valid, but got: {error}");
            }
        }
    }

    #endregion
}
