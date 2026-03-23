namespace RhinoCNCExporter.Core.Profiles;

/// <summary>
/// Machine profile for Biesse CNC machines.
/// Optimized for Biesse CIX format and typical Biesse machine capabilities.
/// </summary>
public class BiesseProfile : MachineProfile
{
    public override double DefaultDz => 18.0; // Common panel thickness for Biesse
    public override double DefaultToolDiameter => 10.0; // Standard Biesse cutter
    public override double GrooveOvertravel => 3.0; // Tighter tolerances
    public override double DefaultPocketStepover => 0.6; // Biesse optimized
    public override double PolyTolerance => 0.01; // Higher precision
    public override int MaxNameLength => 63; // CIX allows longer names
    public override bool UseRntMacro => true; // Biesse supports macro operations
    public override bool UseCornerRounding => true; // Biesse supports corner rounding
    public override string DefaultTech => "T01"; // Biesse tool number format
    public override string FileExtension => ".cix"; // Biesse CIX format
}