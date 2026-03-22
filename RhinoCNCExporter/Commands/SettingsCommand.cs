using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter.Commands;

public sealed class SettingsCommand : Command
{
    private static bool _panelRegistered;

    public override string EnglishName => "RhinoCNCExporterSettings";

    public SettingsCommand()
    {
        // Register panel in Command constructor (Rhino 8 best practice)
        if (!_panelRegistered)
        {
            try
            {
                Panels.RegisterPanel(
                    RhinoCNCExporterPlugIn.Instance,
                    typeof(SettingsPanel),
                    SettingsPanel.PanelDisplayName,
                    null
                );
                _panelRegistered = true;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[RhinoCNCExporter] Settings panel registration failed: {ex.Message}");
            }
        }
    }

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
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
