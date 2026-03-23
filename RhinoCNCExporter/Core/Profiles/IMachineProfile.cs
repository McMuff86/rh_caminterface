namespace RhinoCNCExporter.Core.Profiles;

/// <summary>
/// Interface for machine-specific configuration profiles.
/// Defines defaults and capabilities for different CNC machine types.
/// </summary>
public interface IMachineProfile
{
    /// <summary>Default workpiece thickness (Z-dimension).</summary>
    double DefaultDz { get; }
    
    /// <summary>Default tool diameter for routing operations.</summary>
    double DefaultToolDiameter { get; }
    
    /// <summary>Overtravel distance for groove operations.</summary>
    double GrooveOvertravel { get; }
    
    /// <summary>Default stepover percentage for pocket operations (0.0-1.0).</summary>
    double DefaultPocketStepover { get; }
    
    /// <summary>Tolerance for polyline conversion.</summary>
    double PolyTolerance { get; }
    
    /// <summary>Maximum allowed operation name length.</summary>
    int MaxNameLength { get; }
    
    /// <summary>Whether machine supports RNT macro operations.</summary>
    bool UseRntMacro { get; }
    
    /// <summary>Whether to use corner rounding for polylines.</summary>
    bool UseCornerRounding { get; }
    
    /// <summary>Default technology/tool code.</summary>
    string DefaultTech { get; }
    
    /// <summary>File extension for this machine format.</summary>
    string FileExtension { get; }
}