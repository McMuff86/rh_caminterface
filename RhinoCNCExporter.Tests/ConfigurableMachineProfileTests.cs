using RhinoCNCExporter.Core.Profiles;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class ConfigurableMachineProfileTests
{
    [Fact]
    public void ConfigurableMachineProfile_OverridesOnlySetupOffsets()
    {
        var profile = new ConfigurableMachineProfile(
            new MaestroCadTProfile(),
            setupOffsetX: 5.0,
            setupOffsetY: 7.5);

        Assert.Equal(5.0, profile.SetupOffsetX);
        Assert.Equal(7.5, profile.SetupOffsetY);
        Assert.Equal(19.0, profile.DefaultDz);
        Assert.Equal(".xcs", profile.FileExtension);
    }
}
