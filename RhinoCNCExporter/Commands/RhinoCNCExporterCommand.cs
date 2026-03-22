using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter.Commands;

/// <summary>
/// Main command that opens the dockable RhinoCNC Export panel.
/// Panel registration happens in this command's constructor (not in Plugin.OnLoad!).
/// This follows the Rhino 8 best practice for panel registration.
/// </summary>
public sealed class RhinoCNCExporterCommand : Command
{
    private static bool _panelRegistered;

    public override string EnglishName => "RhinoCNCExporter";

    public RhinoCNCExporterCommand()
    {
        // Register panel in Command constructor — NOT in Plugin.OnLoad()!
        // This is the Rhino 8 recommended pattern (learned from RhinoClaw SentinelChat).
        if (!_panelRegistered)
        {
            try
            {
                Panels.RegisterPanel(
                    RhinoCNCExporterPlugIn.Instance,
                    typeof(ExportPanel),
                    ExportPanel.PanelDisplayName,
                    null  // icon — null uses default; System.Drawing.Common is available if needed
                );
                _panelRegistered = true;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[RhinoCNCExporter] Panel registration failed: {ex.Message}");
            }
        }
    }

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
            Panels.OpenPanel(ExportPanel.PanelId);
            return Result.Success;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[RhinoCNCExporter] Failed to open export panel: {ex.Message}");
            return Result.Failure;
        }
    }
}
