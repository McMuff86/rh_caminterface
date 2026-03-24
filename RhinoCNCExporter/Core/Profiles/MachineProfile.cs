namespace RhinoCNCExporter.Core.Profiles;

/// <summary>
/// Abstract machine profile defining defaults for CNC export.
/// Subclassed per machine type (SCM, Biesse, Homag).
/// </summary>
public abstract class MachineProfile : IMachineProfile
{
    public virtual string MachineKey => "xilog";
    public virtual double DefaultDz => 19.0;
    public virtual double DefaultToolDiameter => 9.5;
    public virtual double GrooveOvertravel => 5.0;
    public virtual double DefaultPocketStepover => 0.7;
    public virtual double PolyTolerance => 0.05;
    public virtual int MaxNameLength => 31;
    public virtual bool UseRntMacro => true;
    public virtual bool UseCornerRounding => false;
    public virtual string DefaultTech => "E010";
    public virtual string FileExtension => ".xcs";
    public virtual double SetupOffsetX => 2.5;
    public virtual double SetupOffsetY => 2.5;
    public virtual double SetupOffsetZ => 0.0;
    public virtual double SetupOffsetRot => 0.0;
}
