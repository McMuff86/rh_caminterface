using System.Collections.Generic;
using RhinoCNCExporter.Core.Blocks;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class MachiningOperationTests
{
    [Fact]
    public void IsEnabled_DefaultsToTrue_WhenFlagIsMissing()
    {
        var operation = new MachiningOperation(CncOperationSchema.TYPE_CONTOUR, new Dictionary<string, string>());

        Assert.True(operation.IsEnabled);
    }

    [Fact]
    public void IsEnabled_ParsesExplicitFalseFlag()
    {
        var operation = new MachiningOperation(
            CncOperationSchema.TYPE_CONTOUR,
            new Dictionary<string, string>
            {
                [CncOperationSchema.CNC_ENABLED] = "false"
            });

        Assert.False(operation.IsEnabled);
    }
}
