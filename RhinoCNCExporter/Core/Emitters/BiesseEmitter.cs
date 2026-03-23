using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Biesse CIX format emitter.
/// Generates Biesse CIX files compatible with BiesseWorks and bSolid.
/// Implements basic operations: Header (MAINDATA), Drill (BG), Cut (ROUTG+GEO).
/// </summary>
public sealed class BiesseEmitter : IEmitter
{
    private readonly NameService _names;
    private int _geoIdCounter = 1001; // Start ID for geometry objects

    public BiesseEmitter(NameService names)
    {
        _names = names;
    }

    /// <summary>CIX file header: ID block and MAINDATA with workpiece dimensions.</summary>
    public string EmitHeader(string programName, double dx, double dy, double dz)
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
        return string.Join("\r\n", lines); // CIX uses Windows line endings
    }

    /// <summary>CIX file footer: empty for basic CIX files.</summary>
    public string EmitFooter()
    {
        return "";
    }

    /// <summary>
    /// Emit a polyline-based routing pass using ROUTG + GEO macros.
    /// Creates geometry definition and routing operation.
    /// </summary>
    public string EmitPolylinePass(string polyName, string opName, IReadOnlyList<(double X, double Y)> pts,
        string tech, double depth, double toolDia, string plane = "Top")
    {
        var geoId = F($"G1003.{_geoIdCounter++}");
        var lines = new List<string>();

        // GEO macro - defines the polyline geometry
        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=GEO");
        lines.Add(F($"\tPARAM,NAME=ID,VALUE=\"{geoId}\""));
        lines.Add("\tPARAM,NAME=SIDE,VALUE=0"); // Top face
        lines.Add("\tPARAM,NAME=CRN,VALUE=\"1\""); // Corner 1
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

        // End geometry definition
        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=ENDPATH");
        lines.Add("END MACRO");
        lines.Add("");

        // ROUTG macro - routing operation that uses the geometry
        lines.Add("BEGIN MACRO");
        lines.Add("\tNAME=ROUTG");
        lines.Add(F($"\tPARAM,NAME=ID,VALUE=\"{polyName}\""));
        lines.Add("\tPARAM,NAME=SIDE,VALUE=0");
        lines.Add("\tPARAM,NAME=CRN,VALUE=\"1\"");
        lines.Add(F($"\tPARAM,NAME=Z,VALUE=0"));
        lines.Add(F($"\tPARAM,NAME=DP,VALUE={depth:F1}"));
        lines.Add(F($"\tPARAM,NAME=DIA,VALUE={toolDia:F1}"));
        lines.Add(F($"\tPARAM,NAME=TNM,VALUE=\"{tech}\""));
        lines.Add("\tPARAM,NAME=CRC,VALUE=2"); // Compensation right
        lines.Add("\tPARAM,NAME=DIR,VALUE=dirCCW"); // Counter-clockwise
        lines.Add(F($"\tPARAM,NAME=GID,VALUE=\"{geoId}\""));
        lines.Add("END MACRO");
        lines.Add("");

        return string.Join("\r\n", lines);
    }

    /// <summary>Emit a single drill operation using BG macro (universal drill).</summary>
    public string EmitDrill(string name, double x, double y, double depth, double dia,
        string plane = "Top", string side = "P")
    {
        var lines = new List<string>
        {
            "BEGIN MACRO",
            "\tNAME=BG",
            F($"\tPARAM,NAME=ID,VALUE=\"{name}\""),
            "\tPARAM,NAME=SIDE,VALUE=0", // Top face
            "\tPARAM,NAME=CRN,VALUE=\"1\"", // Corner 1
            F($"\tPARAM,NAME=X,VALUE={x:F1}"),
            F($"\tPARAM,NAME=Y,VALUE={y:F1}"),
            "\tPARAM,NAME=Z,VALUE=0",
            F($"\tPARAM,NAME=DP,VALUE={depth:F1}"),
            F($"\tPARAM,NAME=DIA,VALUE={dia:F1}"),
            "\tPARAM,NAME=THR,VALUE=YES", // Through drilling
            "\tPARAM,NAME=RTY,VALUE=rpNO", // No repetition
            "END MACRO",
            ""
        };
        return string.Join("\r\n", lines);
    }

    /// <summary>Biesse doesn't use RNT macros like SCM - convert to routing operation.</summary>
    public string EmitRntX(string name, double xStart, double yCenter, double width,
        double xLen, double depth, string code)
    {
        // Create a rectangular groove profile
        var pts = new List<(double X, double Y)>
        {
            (xStart, yCenter - width / 2),
            (xStart + xLen, yCenter - width / 2),
            (xStart + xLen, yCenter + width / 2),
            (xStart, yCenter + width / 2),
            (xStart, yCenter - width / 2) // Close the loop
        };

        return EmitPolylinePass($"{name}_groove", $"{name}_op", pts, code, depth, width, "Top");
    }

    /// <summary>Biesse doesn't use RNT macros like SCM - convert to routing operation.</summary>
    public string EmitRntY(string name, double xCenter, double yStart, double width,
        double yLen, double depth, string code)
    {
        // Create a rectangular groove profile
        var pts = new List<(double X, double Y)>
        {
            (xCenter - width / 2, yStart),
            (xCenter + width / 2, yStart),
            (xCenter + width / 2, yStart + yLen),
            (xCenter - width / 2, yStart + yLen),
            (xCenter - width / 2, yStart) // Close the loop
        };

        return EmitPolylinePass($"{name}_groove", $"{name}_op", pts, code, depth, width, "Top");
    }

    /// <summary>Format helper — ensures invariant culture for decimal formatting.</summary>
    private static string F(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}