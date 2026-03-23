using System.Text.RegularExpressions;

namespace RhinoCNCExporter.Core.LayerParser;

public static class LayerRegex
{
    private static readonly Regex CutRegex = new(
        pattern: @"^CUT_E(?<tech>\d{2,3})(?:_Z(?<depth>\d+(?:\.\d+)?))?(?:_S(?<sd>\d+(?:\.\d+)?))?(?:_D(?<dia>\d+(?:\.\d+)?))?$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryParseCut(string layerName, out CutSpec? spec)
    {
        var m = CutRegex.Match(layerName);
        if (!m.Success)
        {
            spec = null;
            return false;
        }

        var tech = m.Groups["tech"].Value.PadLeft(3, '0');
        double depth = m.Groups["depth"].Success ? double.Parse(m.Groups["depth"].Value) : Defaults.DefaultDz;
        double? sd = m.Groups["sd"].Success ? double.Parse(m.Groups["sd"].Value) : (double?)null;
        double dia = m.Groups["dia"].Success ? double.Parse(m.Groups["dia"].Value) : Defaults.DefaultToolDiameter;
        spec = new CutSpec($"E{tech}", depth, sd, dia);
        return true;
    }

    private static readonly Regex PocketRegex = new(
        pattern: @"^POCKET_E(?<tech>\d{2,3})(?:_Z(?<depth>\d+(?:\.\d+)?))?(?:_S(?<sd>\d+(?:\.\d+)?))?(?:_D(?<dia>\d+(?:\.\d+)?))?(?:_O(?<off>\d+(?:\.\d+)?))?$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryParsePocket(string layerName, out PocketSpec? spec)
    {
        var m = PocketRegex.Match(layerName);
        if (!m.Success)
        {
            spec = null; return false;
        }
        var tech = m.Groups["tech"].Value.PadLeft(3, '0');
        double depth = m.Groups["depth"].Success ? double.Parse(m.Groups["depth"].Value) : Defaults.DefaultDz;
        double? sd = m.Groups["sd"].Success ? double.Parse(m.Groups["sd"].Value) : (double?)null;
        double dia = m.Groups["dia"].Success ? double.Parse(m.Groups["dia"].Value) : Defaults.DefaultToolDiameter;
        double? off = m.Groups["off"].Success ? double.Parse(m.Groups["off"].Value) : (double?)null;
        spec = new PocketSpec($"E{tech}", depth, sd, dia, off);
        return true;
    }

    private static readonly Regex DrillRegex = new(
        pattern: @"^DRILL_D(?<dia>\d+(?:\.\d+)?)(?:_Z(?<depth>\d+(?:\.\d+)?))?(?:_C(?<side>[PL]))?$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryParseDrill(string layerName, out DrillSpec? spec)
    {
        var m = DrillRegex.Match(layerName);
        if (!m.Success) { spec = null; return false; }
        double dia = double.Parse(m.Groups["dia"].Value);
        double depth = m.Groups["depth"].Success ? double.Parse(m.Groups["depth"].Value) : Defaults.DefaultDz;
        char side = m.Groups["side"].Success ? char.ToUpperInvariant(m.Groups["side"].Value[0]) : 'P';
        spec = new DrillSpec(dia, depth, side);
        return true;
    }

    private static readonly Regex RowRegex = new(
        pattern: @"^DRILLROW_D(?<dia>\d+(?:\.\d+)?)(_Z(?<depth>\d+(?:\.\d+)?))?_P(?<pitch>\d+(?:\.\d+)?)(?:_N(?<count>\d+))?$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryParseRow(string layerName, out DrillRowSpec? spec)
    {
        var m = RowRegex.Match(layerName);
        if (!m.Success) { spec = null; return false; }
        double dia = double.Parse(m.Groups["dia"].Value);
        double depth = m.Groups["depth"].Success ? double.Parse(m.Groups["depth"].Value) : Defaults.DefaultDz;
        double pitch = double.Parse(m.Groups["pitch"].Value);
        int? count = m.Groups["count"].Success ? int.Parse(m.Groups["count"].Value) : (int?)null;
        spec = new DrillRowSpec(dia, depth, pitch, count);
        return true;
    }

    private static readonly Regex GrooveChRegex = new(
        pattern: @"^RBNUT_CH_(?<axis>X|Y)_W(?<w>\d+(?:\.\d+)?)(?:_Z(?<depth>\d+(?:\.\d+)?))?(?:_S(?<sd>\d+(?:\.\d+)?))?(?:_E(?<tech>\d{2,3}))?(?:_(?<place>M|P))?$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryParseGrooveChannel(string layerName, out GrooveChannelSpec? spec)
    {
        var m = GrooveChRegex.Match(layerName);
        if (!m.Success) { spec = null; return false; }
        var axis = m.Groups["axis"].Value.ToUpperInvariant() == "X" ? Axis.X : Axis.Y;
        double width = double.Parse(m.Groups["w"].Value);
        double depth = m.Groups["depth"].Success ? double.Parse(m.Groups["depth"].Value) : Defaults.DefaultDz;
        double? sd = m.Groups["sd"].Success ? double.Parse(m.Groups["sd"].Value) : (double?)null;
        string? tech = m.Groups["tech"].Success ? "E" + m.Groups["tech"].Value.PadLeft(3, '0') : null;
        var place = m.Groups["place"].Success && m.Groups["place"].Value.ToUpperInvariant() == "P"
            ? Place.Positive : Place.Center;  // Default M (center) like Python
        spec = new GrooveChannelSpec(axis, width, depth, sd, tech, place);
        return true;
    }

    private static readonly Regex GrooveRntRegex = new(
        pattern: @"^RBNUT_RNT_(?<axis>X|Y)_W(?<w>\d+(?:\.\d+)?)(?:_Z(?<depth>\d+(?:\.\d+)?))?_C(?<code>\d{3})(?:_(?<place>M|P))?$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryParseGrooveRnt(string layerName, out GrooveRntSpec? spec)
    {
        var m = GrooveRntRegex.Match(layerName);
        if (!m.Success) { spec = null; return false; }
        var axis = m.Groups["axis"].Value.ToUpperInvariant() == "X" ? Axis.X : Axis.Y;
        double width = double.Parse(m.Groups["w"].Value);
        double depth = m.Groups["depth"].Success ? double.Parse(m.Groups["depth"].Value) : Defaults.DefaultDz;
        string code = m.Groups["code"].Success ? m.Groups["code"].Value : "";
        var place = m.Groups["place"].Success && m.Groups["place"].Value.ToUpperInvariant() == "P"
            ? Place.Positive : Place.Center;  // Default M (center) like Python
        spec = new GrooveRntSpec(axis, width, depth, code, place);
        return true;
    }

    // --- Drill Pattern (grid array) ---
    // DRILLPAT_D5_Z13_X1_Y4_SX0_SY64
    private static readonly Regex DrillPatRegex = new(
        pattern: @"^DRILLPAT_D(?<dia>\d+(?:\.\d+)?)(?:_Z(?<depth>\d+(?:\.\d+)?))?_X(?<xn>\d+)_Y(?<yn>\d+)_SX(?<sx>\d+(?:\.\d+)?)_SY(?<sy>\d+(?:\.\d+)?)(?:_C(?<side>[PL]))?$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryParseDrillPattern(string layerName, out DrillPatternSpec? spec)
    {
        var m = DrillPatRegex.Match(layerName);
        if (!m.Success) { spec = null; return false; }
        double dia = double.Parse(m.Groups["dia"].Value);
        double depth = m.Groups["depth"].Success ? double.Parse(m.Groups["depth"].Value) : Defaults.DefaultDz;
        int xn = int.Parse(m.Groups["xn"].Value);
        int yn = int.Parse(m.Groups["yn"].Value);
        double sx = double.Parse(m.Groups["sx"].Value);
        double sy = double.Parse(m.Groups["sy"].Value);
        char side = m.Groups["side"].Success ? char.ToUpperInvariant(m.Groups["side"].Value[0]) : 'P';
        spec = new DrillPatternSpec(dia, depth, side, xn, yn, sx, sy);
        return true;
    }

    // --- Horizontal Drill (side drilling) ---
    // HDRILL_D8_Z30_SL  (L=links, R=rechts, V=vorne, H=hinten)
    private static readonly Regex HDrillRegex = new(
        pattern: @"^HDRILL_D(?<dia>\d+(?:\.\d+)?)(?:_Z(?<depth>\d+(?:\.\d+)?))?_S(?<side>[LRVH])$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryParseHorizontalDrill(string layerName, out HorizontalDrillSpec? spec)
    {
        var m = HDrillRegex.Match(layerName);
        if (!m.Success) { spec = null; return false; }
        double dia = double.Parse(m.Groups["dia"].Value);
        double depth = m.Groups["depth"].Success ? double.Parse(m.Groups["depth"].Value) : 30.0; // Default 30mm horizontal
        char side = char.ToUpperInvariant(m.Groups["side"].Value[0]);
        spec = new HorizontalDrillSpec(dia, depth, side);
        return true;
    }
}
