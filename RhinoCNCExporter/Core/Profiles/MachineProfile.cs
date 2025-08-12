namespace RhinoCNCExporter.Core.Profiles;

public abstract class MachineProfile
{
    public virtual double DefaultDz => 19.0;
    public virtual double DefaultToolDiameter => 9.5;
    public virtual double GrooveOvertravel => 5.0;
}
