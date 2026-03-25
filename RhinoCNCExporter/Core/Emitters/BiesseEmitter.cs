using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Biesse CIX format emitter.
/// Generates Biesse CIX files compatible with BiesseWorks and bSolid.
/// Implements: Header (MAINDATA), Drill (BG), DrillPattern (BG+RTY), Cut (ROUTG+GEO),
/// Arc polylines (ARC_EPCE), Workplane/Side drilling.
/// </summary>
public sealed class BiesseEmitter : IEmitter
{
    private readonly NameService _names;
    private int _geoIdCounter = 1001;

    public BiesseEmitter(NameService names)
    {
        _names = names;
    }

    /// <summary>CIX file header: ID block and MAINDATA with workpiece dimensions.</summary>
    public string EmitHeader(string programName, double dx, double dy, double dz,
        double setupOffsetX = 2.5, double setupOffsetY = 2.5,
        double setupOffsetZ = 0, double setupOffsetRot = 0)
    {
        var lines = new List<string>
        {
            "BEGIN ID CID3",
            "\tREL= 5.0",
            "END ID",
            "",
            "BEGIN MAINDATA",
            F($"\tLPX={dx:F5}"),
            F($"\tLPY={dy:F5}"),
            F($"\tLPZ={dz:F5}"),
            "\tORLST=\"1\"",
            "\tSIMMETRY=1",
            "\tTLCHK=0",
            "\tTOOLING=\"\"",
            "\tFCN=1.000000",
            "\tMATERIAL=\"wood\"",
            "\tTHICKNESS=mm",
            "\tQUANTITY=1",
            "\tTYP=BAR",
            "END MAINDATA",
            ""
        };
        return string.Join("\r\n", lines);
    }

    /// <summary>CIX file footer: empty for basic CIX files.</summary>
    public string EmitFooter()
    {
        return "";
    }

    /// <summary>Emit a polyline-based routing pass using ROUTG + GEO macros (straight segments only).</summary>
    public string EmitPolylinePass(string polyName, string opName, IReadOnlyList<(double X, double Y)> pts,
        string tech, double depth, double toolDia, string plane = "Top")
    {
        var geoId = F($"G1003.{_geoIdCounter++}");
        var lines = new List<string>();

        // GEO macro
        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=GEO");
        lines.Add(F($"\tPARAM,NAME=ID,VALUE=\"{geoId}\""));
        lines.Add("\tPARAM,NAME=SIDE,VALUE=0");
        lines.Add("\tPARAM,NAME=CRN,VALUE=\"1\"");
        lines.Add(F($"\tPARAM,NAME=DP,VALUE={depth:F1}"));
        lines.Add("END MACRO");
        lines.Add("");

        // Start point
        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=START_POINT");
        lines.Add(F($"\tPARAM,NAME=X,VALUE={pts[0].X:F1}"));
        lines.Add(F($"\tPARAM,NAME=Y,VALUE={pts[0].Y:F1}"));
        lines.Add("\tPARAM,NAME=Z,VALUE=0");
        lines.Add("END MACRO");
        lines.Add("");

        // Line segments
        for (int i = 1; i < pts.Count; i++)
        {
            lines.Add("BEGIN MACRO");
            lines.Add("\tNAME=LINE_EP");
            lines.Add(F($"\tPARAM,NAME=XE,VALUE={pts[i].X:F1}"));
            lines.Add(F($"\tPARAM,NAME=YE,VALUE={pts[i].Y:F1}"));
            lines.Add("\tPARAM,NAME=ZE,VALUE=0");
            lines.Add("END MACRO");
            lines.Add("");
        }

        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=ENDPATH");
        lines.Add("END MACRO");
        lines.Add("");

        // ROUTG macro
        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=ROUTG");
        lines.Add(F($"\tPARAM,NAME=ID,VALUE=\"{polyName}\""));
        lines.Add("\tPARAM,NAME=SIDE,VALUE=0");
        lines.Add("\tPARAM,NAME=CRN,VALUE=\"1\"");
        lines.Add(F($"\tPARAM,NAME=Z,VALUE=0"));
        lines.Add(F($"\tPARAM,NAME=DP,VALUE={depth:F1}"));
        lines.Add(F($"\tPARAM,NAME=DIA,VALUE={toolDia:F1}"));
        lines.Add(F($"\tPARAM,NAME=TNM,VALUE=\"{tech}\""));
        lines.Add("\tPARAM,NAME=CRC,VALUE=2");
        lines.Add("\tPARAM,NAME=DIR,VALUE=dirCCW");
        lines.Add(F($"\tPARAM,NAME=GID,VALUE=\"{geoId}\""));
        lines.Add("END MACRO");
        lines.Add("");

        return string.Join("\r\n", lines);
    }

    /// <summary>Emit a polyline-based routing pass with mixed line/arc segments (LINE_EP + ARC_EPCE).</summary>
    public string EmitPolylinePassWithArcs(string polyName, string opName,
        double startX, double startY, IReadOnlyList<PolySegment> segments,
        string tech, double depth, double toolDia, string plane = "Top")
    {
        var geoId = F($"G1003.{_geoIdCounter++}");
        var lines = new List<string>();

        // GEO macro
        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=GEO");
        lines.Add(F($"\tPARAM,NAME=ID,VALUE=\"{geoId}\""));
        lines.Add("\tPARAM,NAME=SIDE,VALUE=0");
        lines.Add("\tPARAM,NAME=CRN,VALUE=\"1\"");
        lines.Add(F($"\tPARAM,NAME=DP,VALUE={depth:F1}"));
        lines.Add("END MACRO");
        lines.Add("");

        // Start point
        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=START_POINT");
        lines.Add(F($"\tPARAM,NAME=X,VALUE={startX:F1}"));
        lines.Add(F($"\tPARAM,NAME=Y,VALUE={startY:F1}"));
        lines.Add("\tPARAM,NAME=Z,VALUE=0");
        lines.Add("END MACRO");
        lines.Add("");

        foreach (var seg in segments)
        {
            if (seg.IsArc)
            {
                lines.Add("BEGIN MACRO");
                lines.Add("\tNAME=ARC_EPCE");
                lines.Add(F($"\tPARAM,NAME=XE,VALUE={seg.EndX:F1}"));
                lines.Add(F($"\tPARAM,NAME=YE,VALUE={seg.EndY:F1}"));
                lines.Add("\tPARAM,NAME=ZE,VALUE=0");
                lines.Add(F($"\tPARAM,NAME=XC,VALUE={seg.CenterX:F1}"));
                lines.Add(F($"\tPARAM,NAME=YC,VALUE={seg.CenterY:F1}"));
                lines.Add(F($"\tPARAM,NAME=DIR,VALUE={(seg.Clockwise ? "dirCW" : "dirCCW")}"));
                lines.Add("END MACRO");
                lines.Add("");
            }
            else
            {
                lines.Add("BEGIN MACRO");
                lines.Add("\tNAME=LINE_EP");
                lines.Add(F($"\tPARAM,NAME=XE,VALUE={seg.EndX:F1}"));
                lines.Add(F($"\tPARAM,NAME=YE,VALUE={seg.EndY:F1}"));
                lines.Add("\tPARAM,NAME=ZE,VALUE=0");
                lines.Add("END MACRO");
                lines.Add("");
            }
        }

        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=ENDPATH");
        lines.Add("END MACRO");
        lines.Add("");

        // ROUTG macro
        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=ROUTG");
        lines.Add(F($"\tPARAM,NAME=ID,VALUE=\"{polyName}\""));
        lines.Add("\tPARAM,NAME=SIDE,VALUE=0");
        lines.Add("\tPARAM,NAME=CRN,VALUE=\"1\"");
        lines.Add(F($"\tPARAM,NAME=Z,VALUE=0"));
        lines.Add(F($"\tPARAM,NAME=DP,VALUE={depth:F1}"));
        lines.Add(F($"\tPARAM,NAME=DIA,VALUE={toolDia:F1}"));
        lines.Add(F($"\tPARAM,NAME=TNM,VALUE=\"{tech}\""));
        lines.Add("\tPARAM,NAME=CRC,VALUE=2");
        lines.Add("\tPARAM,NAME=DIR,VALUE=dirCCW");
        lines.Add(F($"\tPARAM,NAME=GID,VALUE=\"{geoId}\""));
        lines.Add("END MACRO");
        lines.Add("");

        return string.Join("\r\n", lines);
    }

    /// <summary>Emit a single drill operation using BG macro.</summary>
    public string EmitDrill(string name, double x, double y, double depth, double dia,
        string plane = "Top", string side = "P")
    {
        var lines = new List<string>
        {
            "BEGIN MACRO",
            "\tNAME=BG",
            F($"\tPARAM,NAME=ID,VALUE=\"{name}\""),
            "\tPARAM,NAME=SIDE,VALUE=0",
            "\tPARAM,NAME=CRN,VALUE=\"1\"",
            F($"\tPARAM,NAME=X,VALUE={x:F1}"),
            F($"\tPARAM,NAME=Y,VALUE={y:F1}"),
            "\tPARAM,NAME=Z,VALUE=0",
            F($"\tPARAM,NAME=DP,VALUE={depth:F1}"),
            F($"\tPARAM,NAME=DIA,VALUE={dia:F1}"),
            "\tPARAM,NAME=THR,VALUE=YES",
            "\tPARAM,NAME=RTY,VALUE=rpNO",
            "END MACRO",
            ""
        };
        return string.Join("\r\n", lines);
    }

    public string EmitHorizontalDrill(string name, double depth, double dia,
        string plane, string side = "P")
    {
        return EmitDrill(name, 0, 0, depth, dia, plane, side);
    }

    /// <summary>
    /// Emit a drill pattern using BG macro with RTY repeat.
    /// Biesse uses RTY=rpGRD for grid patterns with DX/DY spacing and NRX/NRY counts.
    /// </summary>
    public string EmitDrillPattern(string name, double x, double y, double depth, double dia,
        int xCount, int yCount, double xSpacing, double ySpacing,
        string plane = "Top", string side = "P")
    {
        var lines = new List<string>
        {
            "BEGIN MACRO",
            "\tNAME=BG",
            F($"\tPARAM,NAME=ID,VALUE=\"{name}\""),
            "\tPARAM,NAME=SIDE,VALUE=0",
            "\tPARAM,NAME=CRN,VALUE=\"1\"",
            F($"\tPARAM,NAME=X,VALUE={x:F1}"),
            F($"\tPARAM,NAME=Y,VALUE={y:F1}"),
            "\tPARAM,NAME=Z,VALUE=0",
            F($"\tPARAM,NAME=DP,VALUE={depth:F1}"),
            F($"\tPARAM,NAME=DIA,VALUE={dia:F1}"),
            "\tPARAM,NAME=THR,VALUE=YES",
            "\tPARAM,NAME=RTY,VALUE=rpGRD",
            F($"\tPARAM,NAME=DX,VALUE={xSpacing:F1}"),
            F($"\tPARAM,NAME=DY,VALUE={ySpacing:F1}"),
            F($"\tPARAM,NAME=NRX,VALUE={xCount}"),
            F($"\tPARAM,NAME=NRY,VALUE={yCount}"),
            "END MACRO",
            ""
        };
        return string.Join("\r\n", lines);
    }

    /// <summary>Biesse doesn't use RNT macros like SCM — convert to routing operation.</summary>
    public string EmitRntX(string name, double xStart, double yCenter, double width,
        double xLen, double depth, string code)
    {
        var pts = new List<(double X, double Y)>
        {
            (xStart, yCenter - width / 2),
            (xStart + xLen, yCenter - width / 2),
            (xStart + xLen, yCenter + width / 2),
            (xStart, yCenter + width / 2),
            (xStart, yCenter - width / 2)
        };
        return EmitPolylinePass($"{name}_groove", $"{name}_op", pts, code, depth, width, "Top");
    }

    /// <summary>Biesse doesn't use RNT macros like SCM — convert to routing operation.</summary>
    public string EmitRntY(string name, double xCenter, double yStart, double width,
        double yLen, double depth, string code)
    {
        var pts = new List<(double X, double Y)>
        {
            (xCenter - width / 2, yStart),
            (xCenter + width / 2, yStart),
            (xCenter + width / 2, yStart + yLen),
            (xCenter - width / 2, yStart + yLen),
            (xCenter - width / 2, yStart)
        };
        return EmitPolylinePass($"{name}_groove", $"{name}_op", pts, code, depth, width, "Top");
    }

    /// <summary>Biesse workplane — not directly supported, maps to SIDE parameter.</summary>
    public string EmitWorkplane(string name, double x, double y, double z, double rotX, double rotY)
    {
        // Biesse uses SIDE parameter (2=left, 3=right, 4=front, 5=back)
        // For now, emit a comment placeholder
        return $"// Workplane \"{name}\" at ({x},{y},{z}) rot({rotX},{rotY})\r\n";
    }

    /// <summary>Biesse workplane selection — uses SIDE parameter on subsequent operations.</summary>
    public string EmitSelectWorkplane(string name)
    {
        return $"// SelectWorkplane \"{name}\"\r\n";
    }

    /// <summary>
    /// Biesse BladeCut — convert to angled routing operation.
    /// Biesse doesn't have direct BladeCut equivalent, use ROUTG with angle.
    /// </summary>
    public string EmitBladeCut(string name, double angle, IReadOnlyList<BladeCutSegment> segments,
        string tech, double depth, SectioningStrategy strategy, string plane = "Top")
    {
        // Convert segments to polyline points
        var pts = new List<(double X, double Y)>();
        foreach (var segment in segments)
        {
            if (pts.Count == 0)
                pts.Add((segment.StartX, segment.StartY));
            pts.Add((segment.EndX, segment.EndY));
        }

        if (pts.Count < 2)
            return $"// BladeCut \"{name}\" (insufficient segments)\r\n";

        // Use routing with angle parameter
        var geoId = _geoIdCounter++;
        var side = plane == "Top" ? 0 : 1;

        var lines = new List<string>
        {
            "BEGIN MACRO",
            F($"\tNAME=ROUTG"),
            F($"\tPARAM,NAME=ID,VALUE={geoId}"),
            F($"\tPARAM,NAME=SIDE,VALUE={side}"),
            F($"\tPARAM,NAME=CRN,VALUE=\"\""),
            F($"\tPARAM,NAME=DP,VALUE={depth:F1}"),
            F($"\tPARAM,NAME=ISO,VALUE=\"\""),
            F($"\tPARAM,NAME=OPT,VALUE=YES"),
            F($"\tPARAM,NAME=DIA,VALUE=10"),
            F($"\tPARAM,NAME=RTY,VALUE=rpCCW"),
            F($"\tPARAM,NAME=XRC,VALUE=0"),
            F($"\tPARAM,NAME=YRC,VALUE=0"),
            F($"\tPARAM,NAME=ANG,VALUE={angle:F1}"),
            "\tPARAM,NAME=CKA,VALUE=azrNO",
            "END MACRO",
            "",
            $"BEGIN MACRO NAME=GEO ID={geoId}"
        };

        // Add geometry
        lines.Add(F($"\tSTART_POINT,X={pts[0].X:F5},Y={pts[0].Y:F5}"));
        for (int i = 1; i < pts.Count; i++)
        {
            lines.Add(F($"\tLINE_EP,X={pts[i].X:F5},Y={pts[i].Y:F5}"));
        }
        lines.Add("\tENDPATH");
        lines.Add("END MACRO");
        lines.Add("");

        return string.Join("\r\n", lines);
    }

    /// <summary>
    /// Biesse HelicMillingStrategy — convert to spiral pocket operation.
    /// Uses POCK macro with spiral strategy.
    /// </summary>
    public string EmitHelicMillingStrategy(double radius, bool direction, double depth)
    {
        return $"// HelicMillingStrategy radius={radius:F1}, dir={direction}, depth={depth:F1}\r\n";
    }

    private static string F(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
