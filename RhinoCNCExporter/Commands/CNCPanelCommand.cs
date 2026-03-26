using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter.Commands;

/// <summary>
/// Command to open/toggle the dockable CNC Operations panel.
/// Panel registration follows the Rhino 8 best practice (Command constructor).
/// </summary>
public sealed class CNCPanelCommand : Command
{
    private static bool _panelRegistered;

    public override string EnglishName => "CNCPanel";

    public CNCPanelCommand()
    {
        // Register panel in Command constructor — NOT in Plugin.OnLoad()!
        // This is the Rhino 8 recommended pattern (same as RhinoCNCExporterCommand/ExportPanel).
        if (!_panelRegistered)
        {
            try
            {
                Panels.RegisterPanel(
                    RhinoCNCExporterPlugIn.Instance,
                    typeof(CamPanel),
                    CamPanel.PanelDisplayName,
                    null  // icon — null uses default
                );
                _panelRegistered = true;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[RhinoCNCExporter] CAM Panel registration failed: {ex.Message}");
            }
        }
    }

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
            // Toggle: if panel is already visible, close it; otherwise open it
            var panelVisible = Panels.IsPanelVisible(CamPanel.PanelId);
            if (panelVisible)
            {
                Panels.ClosePanel(CamPanel.PanelId);
            }
            else
            {
                Panels.OpenPanel(CamPanel.PanelId);
            }

            return Result.Success;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[RhinoCNCExporter] Failed to toggle CAM panel: {ex.Message}");
            return Result.Failure;
        }
    }
}
