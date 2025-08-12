using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter.Commands;

public sealed class SettingsCommand : Command
{
    public override string EnglishName => "RhinoCNCExporterSettings";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
            // Open the settings panel; if already visible, bring to front
            Panels.OpenPanel(typeof(SettingsPanel).GUID);
            return Result.Success;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[RhinoCNCExporter] Failed to open settings panel: {ex.Message}");
            return Result.Failure;
        }
    }
}
