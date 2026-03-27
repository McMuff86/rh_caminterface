using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for OperationDefaultsBase: built-in machine-profile defaults for all operation types.
/// These tests validate the pure logic without RhinoCommon (no document UserText persistence).
/// </summary>
public class OperationDefaultsTests
{
    #region SCM/Xilog Defaults

    [Fact]
    public void GetDefaults_Contour_Xilog_CorrectValues()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Contour", "xilog");

        Assert.Equal(19.0, defaults.Depth);
        Assert.Equal(3000.0, defaults.Feedrate);
        Assert.Equal(CncOperationSchema.STRATEGY_FINISH, defaults.Strategy);
    }

    [Fact]
    public void GetDefaults_Pocket_Xilog_CorrectValues()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Pocket", "xilog");

        Assert.Equal(5.0, defaults.Depth);
        Assert.Equal(2000.0, defaults.Feedrate);
        Assert.Equal(CncOperationSchema.STRATEGY_ROUGH, defaults.Strategy);
        Assert.Equal(50.0, defaults.Stepover);
        Assert.Equal(CncOperationSchema.RAMP_SPIRAL, defaults.RampEntry);
    }

    [Fact]
    public void GetDefaults_Drill_Xilog_CorrectValues()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Drill", "xilog");

        Assert.Equal(19.0, defaults.Depth);
        Assert.Equal(1500.0, defaults.Feedrate);
        Assert.Equal(5.0, defaults.Diameter);
        Assert.False(defaults.Peck);
        Assert.Equal(5.0, defaults.PeckDepth);
    }

    [Fact]
    public void GetDefaults_Groove_Xilog_CorrectValues()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Groove", "xilog");

        Assert.Equal(8.0, defaults.Depth);
        Assert.Equal(2500.0, defaults.Feedrate);
        Assert.Equal(CncOperationSchema.STRATEGY_FINISH, defaults.Strategy);
        Assert.Equal(4.0, defaults.Width);
    }

    #endregion

    #region Biesse Defaults

    [Fact]
    public void GetDefaults_Contour_Biesse_DifferentFeedrate()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Contour", "biesse");

        Assert.Equal(18.0, defaults.Depth); // Biesse uses 18mm default
        Assert.Equal(4000.0, defaults.Feedrate); // Higher feedrate than SCM
        Assert.Equal(CncOperationSchema.STRATEGY_FINISH, defaults.Strategy);
    }

    [Fact]
    public void GetDefaults_Pocket_Biesse_DifferentStepover()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Pocket", "biesse");

        Assert.Equal(5.0, defaults.Depth);
        Assert.Equal(2500.0, defaults.Feedrate);
        Assert.Equal(45.0, defaults.Stepover); // Biesse uses 45% vs SCM 50%
    }

    [Fact]
    public void GetDefaults_Drill_Biesse_DifferentFeedrate()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Drill", "biesse");

        Assert.Equal(18.0, defaults.Depth); // Biesse uses 18mm
        Assert.Equal(1800.0, defaults.Feedrate); // Higher feedrate than SCM
    }

    [Fact]
    public void GetDefaults_Groove_Biesse_DifferentFeedrate()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Groove", "biesse");

        Assert.Equal(8.0, defaults.Depth);
        Assert.Equal(3000.0, defaults.Feedrate); // Higher than SCM's 2500
    }

    #endregion

    #region SCM vs Biesse Comparison

    [Theory]
    [InlineData("Contour")]
    [InlineData("Pocket")]
    [InlineData("Drill")]
    [InlineData("Groove")]
    public void GetDefaults_Biesse_HasHigherFeedrateThanScm(string operationType)
    {
        var scm = OperationDefaultsBase.GetMachineProfileDefaults(operationType, "xilog");
        var biesse = OperationDefaultsBase.GetMachineProfileDefaults(operationType, "biesse");

        Assert.True(biesse.Feedrate >= scm.Feedrate,
            $"Biesse feedrate ({biesse.Feedrate}) should be >= SCM feedrate ({scm.Feedrate}) for {operationType}");
    }

    [Theory]
    [InlineData("Contour")]
    [InlineData("Drill")]
    public void GetDefaults_Biesse_HasSmallerDefaultDepth(string operationType)
    {
        var scm = OperationDefaultsBase.GetMachineProfileDefaults(operationType, "xilog");
        var biesse = OperationDefaultsBase.GetMachineProfileDefaults(operationType, "biesse");

        Assert.True(biesse.Depth <= scm.Depth,
            $"Biesse depth ({biesse.Depth}) should be <= SCM depth ({scm.Depth}) for {operationType}");
    }

    #endregion

    #region MaestroCadT (SCM-compatible)

    [Fact]
    public void GetDefaults_MaestroCadT_SameAsSCM()
    {
        // MaestroCadT uses SCM-compatible defaults
        var maestro = OperationDefaultsBase.GetMachineProfileDefaults("Contour", "maestrocadt");
        var scm = OperationDefaultsBase.GetMachineProfileDefaults("Contour", "xilog");

        Assert.Equal(scm.Depth, maestro.Depth);
        Assert.Equal(scm.Feedrate, maestro.Feedrate);
        Assert.Equal(scm.Strategy, maestro.Strategy);
    }

    #endregion

    #region Case Insensitivity

    [Theory]
    [InlineData("contour")]
    [InlineData("CONTOUR")]
    [InlineData("Contour")]
    [InlineData("CoNtOuR")]
    public void GetDefaults_CaseInsensitive_OperationType(string operationType)
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults(operationType, "xilog");
        Assert.Equal(19.0, defaults.Depth);
        Assert.Equal(3000.0, defaults.Feedrate);
    }

    [Theory]
    [InlineData("xilog")]
    [InlineData("XILOG")]
    [InlineData("Xilog")]
    public void GetDefaults_CaseInsensitive_MachineKey(string machineKey)
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("Contour", machineKey);
        Assert.Equal(19.0, defaults.Depth);
        Assert.Equal(3000.0, defaults.Feedrate);
    }

    #endregion

    #region Unknown Operation Type

    [Fact]
    public void GetDefaults_UnknownType_ReturnsFallbackDefaults()
    {
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults("UNKNOWN_TYPE", "xilog");

        Assert.Equal(10.0, defaults.Depth);
        Assert.Equal(2000.0, defaults.Feedrate);
    }

    #endregion

    #region All Operation Types Have Defaults

    [Fact]
    public void AllOperationTypes_AreKnown()
    {
        var types = OperationDefaultsBase.AllOperationTypes;

        Assert.Contains("Contour", types);
        Assert.Contains("Pocket", types);
        Assert.Contains("Drill", types);
        Assert.Contains("Groove", types);
        Assert.Equal(4, types.Count);
    }

    [Fact]
    public void AllOperationTypes_HaveDefaults_ForXilog()
    {
        foreach (var type in OperationDefaultsBase.AllOperationTypes)
        {
            var defaults = OperationDefaultsBase.GetMachineProfileDefaults(type, "xilog");
            Assert.True(defaults.Depth > 0, $"{type} should have positive depth");
            Assert.True(defaults.Feedrate > 0, $"{type} should have positive feedrate");
        }
    }

    [Fact]
    public void AllOperationTypes_HaveDefaults_ForBiesse()
    {
        foreach (var type in OperationDefaultsBase.AllOperationTypes)
        {
            var defaults = OperationDefaultsBase.GetMachineProfileDefaults(type, "biesse");
            Assert.True(defaults.Depth > 0, $"{type} should have positive depth");
            Assert.True(defaults.Feedrate > 0, $"{type} should have positive feedrate");
        }
    }

    #endregion

    #region OperationDefaultValues Model

    [Fact]
    public void OperationDefaultValues_Defaults_AreReasonable()
    {
        var values = new OperationDefaultValues();

        Assert.Equal(0, values.Depth);
        Assert.Equal(0, values.Feedrate);
        Assert.Equal(CncOperationSchema.STRATEGY_FINISH, values.Strategy);
        Assert.Null(values.ToolName);
        Assert.Equal(50.0, values.Stepover);
        Assert.Equal(0, values.Width);
        Assert.Equal(0, values.Diameter);
        Assert.Equal(0, values.PeckDepth);
        Assert.False(values.Peck);
        Assert.Equal(CncOperationSchema.RAMP_STRAIGHT, values.RampEntry);
    }

    [Fact]
    public void OperationDefaultValues_AllPropertiesSettable()
    {
        var values = new OperationDefaultValues
        {
            Depth = 19.0,
            Feedrate = 3000.0,
            Strategy = CncOperationSchema.STRATEGY_ROUGH,
            ToolName = "HM Fräser 8mm",
            Stepover = 45.0,
            Width = 5.0,
            Diameter = 8.0,
            PeckDepth = 3.0,
            Peck = true,
            RampEntry = CncOperationSchema.RAMP_SPIRAL
        };

        Assert.Equal(19.0, values.Depth);
        Assert.Equal(3000.0, values.Feedrate);
        Assert.Equal(CncOperationSchema.STRATEGY_ROUGH, values.Strategy);
        Assert.Equal("HM Fräser 8mm", values.ToolName);
        Assert.Equal(45.0, values.Stepover);
        Assert.Equal(5.0, values.Width);
        Assert.Equal(8.0, values.Diameter);
        Assert.Equal(3.0, values.PeckDepth);
        Assert.True(values.Peck);
        Assert.Equal(CncOperationSchema.RAMP_SPIRAL, values.RampEntry);
    }

    #endregion
}
