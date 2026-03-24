namespace RhinoCNCExporter.Core.Profiles;

/// <summary>
/// Decorates a machine profile with runtime-configurable setup offsets.
/// </summary>
public sealed class ConfigurableMachineProfile : IMachineProfile
{
    private readonly IMachineProfile _innerProfile;

    public ConfigurableMachineProfile(
        IMachineProfile innerProfile,
        double? setupOffsetX = null,
        double? setupOffsetY = null,
        double? setupOffsetZ = null,
        double? setupOffsetRot = null)
    {
        _innerProfile = innerProfile ?? throw new ArgumentNullException(nameof(innerProfile));
        SetupOffsetX = setupOffsetX ?? innerProfile.SetupOffsetX;
        SetupOffsetY = setupOffsetY ?? innerProfile.SetupOffsetY;
        SetupOffsetZ = setupOffsetZ ?? innerProfile.SetupOffsetZ;
        SetupOffsetRot = setupOffsetRot ?? innerProfile.SetupOffsetRot;
    }

    public double DefaultDz => _innerProfile.DefaultDz;
    public string MachineKey => _innerProfile.MachineKey;
    public double DefaultToolDiameter => _innerProfile.DefaultToolDiameter;
    public double GrooveOvertravel => _innerProfile.GrooveOvertravel;
    public double DefaultPocketStepover => _innerProfile.DefaultPocketStepover;
    public double PolyTolerance => _innerProfile.PolyTolerance;
    public int MaxNameLength => _innerProfile.MaxNameLength;
    public bool UseRntMacro => _innerProfile.UseRntMacro;
    public bool UseCornerRounding => _innerProfile.UseCornerRounding;
    public string DefaultTech => _innerProfile.DefaultTech;
    public string FileExtension => _innerProfile.FileExtension;
    public double SetupOffsetX { get; }
    public double SetupOffsetY { get; }
    public double SetupOffsetZ { get; }
    public double SetupOffsetRot { get; }
}
