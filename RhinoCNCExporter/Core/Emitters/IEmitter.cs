using System.Collections.Generic;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Interface for CNC code emitters (XCS, CIX, etc.).
/// Abstracts all emitter operations to allow multiple machine formats.
/// </summary>
public interface IEmitter
{
    /// <summary>Emit file header with program name and workpiece dimensions.</summary>
    string EmitHeader(string programName, double dx, double dy, double dz);
    
    /// <summary>Emit file footer (e.g., XPARK macro).</summary>
    string EmitFooter();
    
    /// <summary>
    /// Emit a polyline-based routing pass (CUT, POCKET ring, GROOVE channel).
    /// </summary>
    /// <param name="polyName">Polyline object name.</param>
    /// <param name="opName">Operation name.</param>
    /// <param name="pts">Polyline points.</param>
    /// <param name="tech">Technology/tool code.</param>
    /// <param name="depth">Cutting depth.</param>
    /// <param name="toolDia">Tool diameter.</param>
    /// <param name="plane">Work plane (e.g., "Top").</param>
    string EmitPolylinePass(string polyName, string opName, IReadOnlyList<(double X, double Y)> pts,
        string tech, double depth, double toolDia, string plane = "Top");
    
    /// <summary>Emit a single drill operation.</summary>
    /// <param name="name">Operation name.</param>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <param name="depth">Drill depth.</param>
    /// <param name="dia">Drill diameter.</param>
    /// <param name="plane">Work plane.</param>
    /// <param name="side">Side identifier (e.g., "P").</param>
    string EmitDrill(string name, double x, double y, double depth, double dia,
        string plane = "Top", string side = "P");
    
    /// <summary>Emit an RNT groove macro (X-axis).</summary>
    /// <param name="name">Operation name.</param>
    /// <param name="xStart">X start position.</param>
    /// <param name="yCenter">Y center position.</param>
    /// <param name="width">Groove width.</param>
    /// <param name="xLen">X length.</param>
    /// <param name="depth">Groove depth.</param>
    /// <param name="code">Technology code.</param>
    string EmitRntX(string name, double xStart, double yCenter, double width,
        double xLen, double depth, string code);
    
    /// <summary>Emit an RNT groove macro (Y-axis).</summary>
    /// <param name="name">Operation name.</param>
    /// <param name="xCenter">X center position.</param>
    /// <param name="yStart">Y start position.</param>
    /// <param name="width">Groove width.</param>
    /// <param name="yLen">Y length.</param>
    /// <param name="depth">Groove depth.</param>
    /// <param name="code">Technology code.</param>
    string EmitRntY(string name, double xCenter, double yStart, double width,
        double yLen, double depth, string code);
}