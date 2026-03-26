namespace RhinoCNCExporter.Core.Profiles;

/// <summary>
/// Machine profile for SCM CNC machines (Xilog format).
/// Default values matching typical SCM Morbidelli/Accord setups.
/// </summary>
public class ScmProfile : MachineProfile
{
    public override string MachineKey => "xilog";
    public override string FileExtension => ".xcs";
}
