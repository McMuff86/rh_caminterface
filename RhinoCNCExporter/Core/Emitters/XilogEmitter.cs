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
/// </summary>
public sealed class XilogEmitter
{
    private readonly NameService _names;

    public XilogEmitter(NameService names)
    {
        _names = names;
    }

    /// <summary>XCS file header: comment, machining params, workpiece box, DZ variable, setup offsets.</summary>
    public string EmitHeader(string programName, double dx, double dy, double dz)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// *** Programm created by Rhino→Maestro Generator ***");
        sb.AppendLine("SetMachiningParameters(\"IJ\",1,10,196608,false);");
        sb.AppendLine(F($"CreateFinishedWorkpieceBox(\"{programName}\", {dx:F3}, {dy:F3}, {dz:F3});"));
        sb.AppendLine(F($"double DZ = {dz:F3};"));
        sb.AppendLine(F($"SetWorkpieceSetupPosition({Defaults.SetupOffsetX},{Defaults.SetupOffsetY},{Defaults.SetupOffsetZ},{Defaults.SetupOffsetRot});"));
        return sb.ToString();
    }

    /// <summary>XCS file footer: XPARK macro.</summary>
    public string EmitFooter()
    {
        return "CreateMacro(\"Wegfahrschritt\",\"XPARK\");\n";
    }

    /// <summary>
    /// Emit a polyline-based routing pass (CUT, POCKET ring, GROOVE channel).
    /// Matches xcs_polyline_pass() from Python exactly.
    /// </summary>
    public string EmitPolylinePass(string polyName, string opName, IReadOnlyList<(double X, double Y)> pts,
        string tech, double depth, double toolDia, string plane = "Top")
    {
        var sb = new StringBuilder();
        sb.AppendLine(F($"SelectWorkplane(\"{plane}\");"));
        sb.AppendLine(F($"CreatePolyline(\"{polyName}\", {pts[0].X:F3},{pts[0].Y:F3});"));

        for (int i = 1; i < pts.Count; i++)
            sb.AppendLine(F($"AddSegmentToPolyline({pts[i].X:F3},{pts[i].Y:F3});"));

        // USE_CORNER_ROUNDING = false in Python reference → skip CreateIso

        sb.AppendLine("SetCompensationMode(false);");
        sb.AppendLine("SetApproachStrategy(false,true,2);");
        sb.AppendLine("SetRetractStrategy(false,true,2.0,2);");
        sb.AppendLine("SetPneumaticHoodPosition(null);");
        sb.AppendLine(F($"CreateRoughFinish(\"{opName}\",{depth:F3},\"\", TypeOfProcess.GeneralRouting ,\"{tech}\",\"-1\",2,-1,-1,-1,0);"));
        sb.AppendLine("ResetApproachStrategy();");
        sb.AppendLine("ResetRetractStrategy();");
        return sb.ToString();
    }

    /// <summary>
    /// Emit a single drill operation.
    /// Matches xcs_drill() from Python exactly.
    /// </summary>
    public string EmitDrill(string name, double x, double y, double depth, double dia,
        string plane = "Top", string side = "P")
    {
        var sb = new StringBuilder();
        sb.AppendLine(F($"SelectWorkplane(\"{plane}\");"));
        sb.AppendLine(F($"CreateDrill(\"{name}\",{x:F3},{y:F3},{depth:F3},{dia:F3},\"\",TypeOfProcess.Drilling,\"-1\",\"-1\",1,-1,-1,\"{side}\");"));
        sb.AppendLine("ResetPattern();");
        return sb.ToString();
    }

    /// <summary>
    /// Emit an RNT groove macro (X-axis).
    /// Matches RNT_TEMPLATE_X from Python exactly.
    /// </summary>
    public string EmitRntX(string name, double xStart, double yCenter, double width,
        double xLen, double depth, string code)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SelectWorkplane(\"Top\");");
        sb.AppendLine(F($"CreateMacro(\"{name}\",\"RNT\",{xStart:F3},{yCenter:F3},{width:F3},-1,-1,-1,{xLen:F3},{depth:F3},true,\"{code}\",\"-1\",false,false,true,{yCenter:F3},null,null,null,null,true);"));
        return sb.ToString();
    }

    /// <summary>
    /// Emit an RNT groove macro (Y-axis).
    /// Matches RNT_TEMPLATE_Y from Python exactly.
    /// </summary>
    public string EmitRntY(string name, double xCenter, double yStart, double width,
        double yLen, double depth, string code)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SelectWorkplane(\"Top\");");
        sb.AppendLine(F($"CreateMacro(\"{name}\",\"RNT\",{xCenter:F3},{yStart:F3},{width:F3},-1,-1,-1,{yLen:F3},{depth:F3},true,\"{code}\",\"-1\",false,false,true,{xCenter:F3},null,null,null,null,true);"));
        return sb.ToString();
    }

    /// <summary>Format helper — ensures invariant culture for decimal formatting.</summary>
    private static string F(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
