using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// SCM / Maestro XCS format emitter.
/// Generates Xilog Script (.xcs) compatible with Maestro CAD+T.
/// Production-quality output matching real CAD+T/Maestro files.
/// Uses Unix line endings (\n).
/// </summary>
public sealed class XilogEmitter : IEmitter
{
    private readonly NameService _names;

    public XilogEmitter(NameService names)
    {
        _names = names;
    }

    /// <summary>
    /// XCS file header — production format with comment blocks.
    /// DZ uses integer format when whole number (19 not 19.000).
    /// Setup offsets use compact format (0 not 0.0, 2.5 not 2.500).
    /// </summary>
    public string EmitHeader(string programName, double dx, double dy, double dz,
        double setupOffsetX = 2.5, double setupOffsetY = 2.5,
        double setupOffsetZ = 0, double setupOffsetRot = 0)
    {
        var lines = new List<string>
        {
            "// *** Programm created by RhinoCNCExporter ***",
            "//**********************************************************",
            "//**********************************************************",
            "// *** Programmparameter setzen *** ",
            "SetMachiningParameters(\"IJ\",1,10,196608,false);",
            "//**********************************************************",
            "//**********************************************************",
            "// *** Bauteil erstellen ***",
            F($"CreateFinishedWorkpieceBox(\"{programName}\", {FmtCompact(dx)}, {FmtCompact(dy)}, {FmtCompact(dz)});"),
            "//**********************************************************",
            "//**********************************************************",
            "// *** Bauteil Infos ***",
            $"//CreateMessage(\"Projekt\",\"projekt_name\",false,false);",
            $"//CreateMessage(\"Datei\",\"{programName}.xcs\",false,false);",
            $"//CreateMessage(\"Bemerkung\",\" \",false,false);",
            "//**********************************************************",
            "//**********************************************************",
            F($"double DZ = {FmtCompact(dz)};"),
            "//AddVariable(\"Entnehmen\",0,0,1,\"\",false,true);",
            "//**********************************************************",
            "//**********************************************************",
            "// *** Bauteil Offsets ***",
            F($"SetWorkpieceSetupPosition({FmtCompact(setupOffsetX)},{FmtCompact(setupOffsetY)},{FmtCompact(setupOffsetZ)},{FmtCompact(setupOffsetRot)});"),
            "//**********************************************************",
            "//**********************************************************",
            "" // trailing empty line
        };
        return string.Join("\n", lines);
    }

    /// <summary>XCS file footer — production format with XPARK and comment blocks.</summary>
    public string EmitFooter()
    {
        var lines = new List<string>
        {
            "//**********************************************************",
            "//**********************************************************",
            "",
            "// Macro RNT",
            "CreateMacro(\"Wegfahrschritt\",\"XPARK\");",
            "//**********************************************************",
            "//**********************************************************",
            "// *** Programm Ende ***",
            ""
        };
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Emit a polyline-based routing pass (CUT, POCKET ring, GROOVE channel).
    /// Supports only straight segments.
    /// </summary>
    public string EmitPolylinePass(string polyName, string opName, IReadOnlyList<(double X, double Y)> pts,
        string tech, double depth, double toolDia, string plane = "Top")
    {
        var lines = new List<string>
        {
            F($"SelectWorkplane(\"{plane}\");"),
            F($"CreatePolyline(\"{polyName}\", {pts[0].X:F3},{pts[0].Y:F3});")
        };

        for (int i = 1; i < pts.Count; i++)
            lines.Add(F($"AddSegmentToPolyline({pts[i].X:F3},{pts[i].Y:F3});"));

        lines.Add("SetCompensationMode(false);");
        lines.Add("SetApproachStrategy(false,true,2);");
        lines.Add("SetRetractStrategy(false,true,2.0,2);");
        lines.Add("SetPneumaticHoodPosition(null);");
        lines.Add(F($"CreateRoughFinish(\"{opName}\",{depth:F3},\"\", TypeOfProcess.GeneralRouting ,\"{tech}\",\"-1\",2,-1,-1,-1,0);"));
        lines.Add("ResetApproachStrategy();");
        lines.Add("ResetRetractStrategy();");
        lines.Add("");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Emit a polyline-based routing pass with mixed line/arc segments.
    /// Uses AddArc2PointCenterToPolyline for arc segments.
    /// </summary>
    public string EmitPolylinePassWithArcs(string polyName, string opName,
        double startX, double startY, IReadOnlyList<PolySegment> segments,
        string tech, double depth, double toolDia, string plane = "Top")
    {
        var lines = new List<string>
        {
            F($"SelectWorkplane(\"{plane}\");"),
            F($"CreatePolyline(\"{polyName}\", {startX:F3},{startY:F3});")
        };

        foreach (var seg in segments)
        {
            if (seg.IsArc)
            {
                lines.Add(F($"AddArc2PointCenterToPolyline({seg.EndX:F3},{seg.EndY:F3},{seg.CenterX:F3},{seg.CenterY:F3},{(seg.Clockwise ? "true" : "false")});"));
            }
            else
            {
                lines.Add(F($"AddSegmentToPolyline({seg.EndX:F3},{seg.EndY:F3});"));
            }
        }

        lines.Add("SetCompensationMode(false);");
        lines.Add("SetApproachStrategy(false,true,2);");
        lines.Add("SetRetractStrategy(false,true,2.0,2);");
        lines.Add("SetPneumaticHoodPosition(null);");
        lines.Add(F($"CreateRoughFinish(\"{opName}\",{depth:F3},\"\", TypeOfProcess.GeneralRouting ,\"{tech}\",\"-1\",2,-1,-1,-1,0);"));
        lines.Add("ResetApproachStrategy();");
        lines.Add("ResetRetractStrategy();");
        lines.Add("");

        return string.Join("\n", lines);
    }

    /// <summary>Emit a single drill operation.</summary>
    public string EmitDrill(string name, double x, double y, double depth, double dia,
        string plane = "Top", string side = "P")
    {
        var lines = new List<string>
        {
            F($"SelectWorkplane(\"{plane}\");"),
            F($"CreateDrill(\"{name}\",{x:F3},{y:F3},{depth:F3},{dia:F3},\"\",TypeOfProcess.Drilling,\"-1\",\"-1\",1,-1,-1,\"{side}\");"),
            "ResetPattern();",
            ""
        };
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Emit a horizontal drill on a custom workplane.
    /// Production format adds two trailing zero parameters and uses an empty tool-string field.
    /// </summary>
    public string EmitHorizontalDrill(string name, double depth, double dia,
        string plane, string side = "P")
    {
        var lines = new List<string>
        {
            F($"SelectWorkplane(\"{plane}\");"),
            F($"CreateDrill(\"{name}\",0.000,0.000,{depth:F3},{dia:F3},\"\",TypeOfProcess.Drilling,\"\",\"-1\",1,-1,-1,\"{side}\",0,0);"),
            "ResetPattern();",
            ""
        };
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Emit a drill pattern (grid array).
    /// Production format (CAD+T Staub / Mittelseite references):
    /// CreatePattern(xCount, yCount, xSpacing, ySpacing, angle, direction) → CreateDrill → ResetPattern.
    /// </summary>
    public string EmitDrillPattern(string name, double x, double y, double depth, double dia,
        int xCount, int yCount, double xSpacing, double ySpacing,
        string plane = "Top", string side = "P")
    {
        var lines = new List<string>
        {
            F($"SelectWorkplane(\"{plane}\");"),
            F($"CreatePattern({xCount},{yCount},{FmtCompact(xSpacing)},{FmtCompact(ySpacing)},0,90);"),
            F($"CreateDrill(\"{name}\",{x:F3},{y:F3},{depth:F3},{dia:F3},\"\",TypeOfProcess.Drilling,\"-1\",\"-1\",1,-1,-1,\"{side}\");"),
            "ResetPattern();",
            ""
        };
        return string.Join("\n", lines);
    }

    /// <summary>Emit an RNT groove macro (X-axis).</summary>
    public string EmitRntX(string name, double xStart, double yCenter, double width,
        double xLen, double depth, string code)
    {
        var lines = new List<string>
        {
            "SelectWorkplane(\"Top\");",
            F($"CreateMacro(\"{name}\",\"RNT\",{xStart:F3},{yCenter:F3},{width:F3},-1,-1,-1,{xLen:F3},{depth:F3},true,\"{code}\",\"-1\",false,false,true,{yCenter:F3},null,null,null,null,true);"),
            ""
        };
        return string.Join("\n", lines);
    }

    /// <summary>Emit an RNT groove macro (Y-axis).</summary>
    public string EmitRntY(string name, double xCenter, double yStart, double width,
        double yLen, double depth, string code)
    {
        var lines = new List<string>
        {
            "SelectWorkplane(\"Top\");",
            F($"CreateMacro(\"{name}\",\"RNT\",{xCenter:F3},{yStart:F3},{width:F3},-1,-1,-1,{yLen:F3},{depth:F3},true,\"{code}\",\"-1\",false,false,true,{xCenter:F3},null,null,null,null,true);"),
            ""
        };
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Create a named workplane at given position and rotation.
    /// Used for horizontal (side) drilling.
    /// Z coordinate typically uses -9.5+DZ for mid-plate.
    /// </summary>
    public string EmitWorkplane(string name, double x, double y, double z, double rotX, double rotY)
    {
        return F($"CreateWorkplane(\"{name}\",{FmtCompact(x)},{FmtCompact(y)},{FmtCompact(z)},{FmtCompact(rotX)},{FmtCompact(rotY)});") + "\n";
    }

    /// <summary>Select a previously created workplane by name.</summary>
    public string EmitSelectWorkplane(string name)
    {
        return F($"SelectWorkplane(\"{name}\");") + "\n";
    }

    /// <summary>
    /// Emit a complete BladeCut operation with SectioningMillingStrategy + Segments + BladeCut.
    /// Production format based on NEW_Schubladen_Doppel_1.xcs reference.
    /// </summary>
    public string EmitBladeCut(string name, double angle, IReadOnlyList<BladeCutSegment> segments,
        string tech, double depth, SectioningStrategy strategy, string plane = "Top")
    {
        var lines = new List<string>
        {
            F($"SelectWorkplane(\"{plane}\");"),
            F($"CreateSectioningMillingStrategy({strategy.StrategyType},{FmtCompact(strategy.OffsetX)},{FmtCompact(strategy.OffsetY)});"),
            "SetApproachStrategy(true,true,0);",
            "SetRetractStrategy(true,true,0,0);"
        };

        // Add all segments
        foreach (var segment in segments)
        {
            lines.Add(F($"CreateSegment(\"{segment.Name}\",{segment.StartX:F3},{segment.StartY:F3},{segment.EndX:F3},{segment.EndY:F3});"));
        }

        // BladeCut with production format parameters
        lines.Add(F($"CreateBladeCut(\"{name}\",\"Blade Cut\",TypeOfProcess.GeneralRouting,\"{tech}\",\"-1\",{angle:F2},2,-1,-1,-1,2,true,true,0,{FmtCompact(depth)});"));
        lines.Add("ResetApproachStrategy();");
        lines.Add("ResetRetractStrategy();");
        lines.Add("");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Emit CreateHelicMillingStrategy for spiral machining.
    /// Used before Rectangle macros for large cutouts.
    /// </summary>
    public string EmitHelicMillingStrategy(double radius, bool direction, double depth)
    {
        return F($"CreateHelicMillingStrategy({FmtCompact(radius)},{(direction ? "true" : "false")},{FmtCompact(depth)});") + "\n";
    }

    /// <summary>Format helper — ensures invariant culture for decimal formatting.</summary>
    private static string F(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Format a number in compact production style:
    /// Integer values → no decimal (19, not 19.000).
    /// Fractional values → minimal decimals (2.5, not 2.500).
    /// Matches CAD+T production output.
    /// </summary>
    private static string FmtCompact(double v)
    {
        // If it's a whole number, emit without decimals
        if (v == Math.Truncate(v))
            return ((long)v).ToString(CultureInfo.InvariantCulture);

        // Otherwise use G format which trims trailing zeros
        return v.ToString("G", CultureInfo.InvariantCulture);
    }
}
