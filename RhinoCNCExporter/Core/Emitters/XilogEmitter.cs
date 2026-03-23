using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// SCM / Maestro XCS format emitter.
/// Generates Xilog Script (.xcs) compatible with Maestro CAD+T.
/// All output matches the Python reference (RH_caminterface_v007.py) exactly.
/// Uses Unix line endings (\n) to match reference output.
/// </summary>
public sealed class XilogEmitter : IEmitter
{
    private readonly NameService _names;

    public XilogEmitter(NameService names)
    {
        _names = names;
    }

    /// <summary>XCS file header: comment, machining params, workpiece box, DZ variable, setup offsets.</summary>
    public string EmitHeader(string programName, double dx, double dy, double dz)
    {
        var lines = new List<string>
        {
            "// *** Programm created by Rhino→Maestro Generator ***",
            "SetMachiningParameters(\"IJ\",1,10,196608,false);",
            F($"CreateFinishedWorkpieceBox(\"{programName}\", {dx:F3}, {dy:F3}, {dz:F3});"),
            F($"double DZ = {dz:F3};"),
            $"SetWorkpieceSetupPosition({FmtSetup(Defaults.SetupOffsetX)},{FmtSetup(Defaults.SetupOffsetY)},{FmtSetup(Defaults.SetupOffsetZ)},{FmtSetup(Defaults.SetupOffsetRot)});",
            "" // trailing empty line (matches Python's '' at end of list)
        };
        return string.Join("\n", lines);
    }

    /// <summary>XCS file footer: XPARK macro.</summary>
    public string EmitFooter()
    {
        return "CreateMacro(\"Wegfahrschritt\",\"XPARK\");\n";
    }

    /// <summary>
    /// Emit a polyline-based routing pass (CUT, POCKET ring, GROOVE channel).
    /// Matches xcs_polyline_pass() from Python exactly.
    /// Python returns "\n".join([lines..., '']) which gives trailing \n.
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

        // USE_CORNER_ROUNDING = false in Python reference → skip CreateIso

        lines.Add("SetCompensationMode(false);");
        lines.Add("SetApproachStrategy(false,true,2);");
        lines.Add("SetRetractStrategy(false,true,2.0,2);");
        lines.Add("SetPneumaticHoodPosition(null);");
        lines.Add(F($"CreateRoughFinish(\"{opName}\",{depth:F3},\"\", TypeOfProcess.GeneralRouting ,\"{tech}\",\"-1\",2,-1,-1,-1,0);"));
        lines.Add("ResetApproachStrategy();");
        lines.Add("ResetRetractStrategy();");
        lines.Add(""); // trailing empty → produces trailing \n via join

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Emit a single drill operation.
    /// Matches xcs_drill() from Python exactly.
    /// </summary>
    public string EmitDrill(string name, double x, double y, double depth, double dia,
        string plane = "Top", string side = "P")
    {
        var lines = new List<string>
        {
            F($"SelectWorkplane(\"{plane}\");"),
            F($"CreateDrill(\"{name}\",{x:F3},{y:F3},{depth:F3},{dia:F3},\"\",TypeOfProcess.Drilling,\"-1\",\"-1\",1,-1,-1,\"{side}\");"),
            "ResetPattern();",
            "" // trailing empty
        };
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Emit an RNT groove macro (X-axis).
    /// Matches RNT_TEMPLATE_X from Python exactly.
    /// </summary>
    public string EmitRntX(string name, double xStart, double yCenter, double width,
        double xLen, double depth, string code)
    {
        // Python: parts.append('SelectWorkplane("Top");'); parts.append(RNT_TEMPLATE_X.format(...)); parts.append('')
        var lines = new List<string>
        {
            "SelectWorkplane(\"Top\");",
            F($"CreateMacro(\"{name}\",\"RNT\",{xStart:F3},{yCenter:F3},{width:F3},-1,-1,-1,{xLen:F3},{depth:F3},true,\"{code}\",\"-1\",false,false,true,{yCenter:F3},null,null,null,null,true);"),
            "" // trailing empty
        };
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Emit an RNT groove macro (Y-axis).
    /// Matches RNT_TEMPLATE_Y from Python exactly.
    /// </summary>
    public string EmitRntY(string name, double xCenter, double yStart, double width,
        double yLen, double depth, string code)
    {
        var lines = new List<string>
        {
            "SelectWorkplane(\"Top\");",
            F($"CreateMacro(\"{name}\",\"RNT\",{xCenter:F3},{yStart:F3},{width:F3},-1,-1,-1,{yLen:F3},{depth:F3},true,\"{code}\",\"-1\",false,false,true,{xCenter:F3},null,null,null,null,true);"),
            "" // trailing empty
        };
        return string.Join("\n", lines);
    }

    /// <summary>Format helper — ensures invariant culture for decimal formatting.</summary>
    private static string F(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    /// <summary>Format setup offset value like Python's default float repr (2.5→"2.5", 0.0→"0.0").</summary>
    private static string FmtSetup(double v)
    {
        var s = v.ToString(CultureInfo.InvariantCulture);
        if (!s.Contains('.')) s += ".0";
        return s;
    }
}
