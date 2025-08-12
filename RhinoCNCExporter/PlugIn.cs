using System;
using Rhino;
using Rhino.PlugIns;
using Rhino.UI;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter;

public sealed class RhinoCNCExporterPlugIn : PlugIn
{
    public static RhinoCNCExporterPlugIn Instance { get; private set; } = null!;

    // Unique plugin identifier; must remain stable
    public static readonly Guid PluginId = new("2e8c8a7c-1bcb-4b0d-8a56-4b2b6f0d7f6e");

    public RhinoCNCExporterPlugIn()
    {
        Instance = this;
    }

    public override Rhino.PlugIns.PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        // Register Settings panel (Eto)
        try
        {
            Panels.RegisterPanel(this, typeof(SettingsPanel), SettingsPanel.PanelDisplayName, null);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[RhinoCNCExporter] Failed to register panel: {ex.Message}");
        }
        return LoadReturnCode.Success;
    }

    protected override void OnShutdown()
    {
        base.OnShutdown();
    }
}
