using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Pipeline;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class ExportModeResolverTests
{
    [Fact]
    public void Automatic_With3DPlates_PrefersMultiPlate()
    {
        var capabilities = new DocumentCapabilities
        {
            Has3DPlates = true,
            HasBlocks = true,
            PlateCount = 4
        };

        var decision = ExportModeResolver.Decide(capabilities, ExportMode.Automatic);

        Assert.True(decision.IsExecutable);
        Assert.Equal(ExportMode.MultiPlate3D, decision.ResolvedMode);
    }

    [Fact]
    public void Automatic_WithWkPiece_PrefersLegacy()
    {
        var capabilities = new DocumentCapabilities
        {
            HasLegacyPiece = true,
            HasLegacyMachiningLayers = true
        };

        var decision = ExportModeResolver.Decide(capabilities, ExportMode.Automatic);

        Assert.True(decision.IsExecutable);
        Assert.Equal(ExportMode.LegacyOnly, decision.ResolvedMode);
    }

    [Fact]
    public void ForcedMultiPlate_Without3DPlates_IsNotExecutable()
    {
        var capabilities = new DocumentCapabilities
        {
            HasLegacyPiece = true,
            HasBlocks = true
        };

        var decision = ExportModeResolver.Decide(capabilities, ExportMode.MultiPlate3D);

        Assert.False(decision.IsExecutable);
        Assert.Equal(ExportMode.MultiPlate3D, decision.ResolvedMode);
    }

    [Fact]
    public void ForcedLegacy_WithoutWkPiece_IsNotExecutable()
    {
        var capabilities = new DocumentCapabilities
        {
            HasBlocks = true
        };

        var decision = ExportModeResolver.Decide(capabilities, ExportMode.LegacyOnly);

        Assert.False(decision.IsExecutable);
        Assert.Equal(ExportMode.LegacyOnly, decision.ResolvedMode);
    }
}
