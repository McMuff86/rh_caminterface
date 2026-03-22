using System;
using System.Runtime.InteropServices;
using System.Globalization;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoCNCExporter.UI;

[Guid("c55c3e2e-8f12-4a3c-88a3-bf4c9e0c2a86")]
public sealed class SettingsPanel : Panel
{
    public static readonly Guid PanelId = typeof(SettingsPanel).GUID;
    public const string PanelDisplayName = "RhinoCNCExporter Settings";

    private readonly TextBox defaultDzTextBox;
    private readonly TextBox defaultToolDiameterTextBox;
    private readonly CheckBox isoPerOpCheckBox;
    private readonly Button quickExportButton;

    public SettingsPanel()
    {
        defaultDzTextBox = new TextBox { PlaceholderText = "Default DZ (mm)", Text = "19" };
        defaultToolDiameterTextBox = new TextBox { PlaceholderText = "Default Tool Ø (mm)", Text = "9.5" };
        isoPerOpCheckBox = new CheckBox { Text = "Emit ISO per operation", Checked = false };

        var saveButton = new Button { Text = "Save" };
        saveButton.Click += (_, _) => SaveSettings();

        quickExportButton = new Button { Text = "Quick Export (.xcs)" };
        quickExportButton.Click += (_, _) => QuickExport();

        Content = new StackLayout
        {
            Padding = new Padding(10),
            Spacing = 8,
            Items =
            {
                new Label { Text = PanelDisplayName, Font = new Font(SystemFont.Bold, 12) },
                new StackLayoutItem(new Label { Text = "Default DZ (mm)" }),
                defaultDzTextBox,
                new StackLayoutItem(new Label { Text = "Default Tool Ø (mm)" }),
                defaultToolDiameterTextBox,
                isoPerOpCheckBox,
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Items = { saveButton, quickExportButton }
                }
            }
        };
    }

    private void SaveSettings()
    {
        // Stub: persist settings later (e.g., PlugIn.Settings + %APPDATA%)
        _ = double.TryParse(defaultDzTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        _ = double.TryParse(defaultToolDiameterTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        _ = isoPerOpCheckBox.Checked;
    }

    private void QuickExport()
    {
        var doc = Rhino.RhinoDoc.ActiveDoc;
        if (doc is null)
            return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Xilog",
            Filters = { new FileFilter("Xilog Program (*.xcs)", ".xcs") },
            FileName = string.IsNullOrWhiteSpace(doc.Name) ? "program.xcs" : System.IO.Path.ChangeExtension(doc.Name, ".xcs")
        };

        if (dlg.ShowDialog(this) != DialogResult.Ok || string.IsNullOrWhiteSpace(dlg.FileName))
            return;

        var ok = Services.ExportService.ExportXilog(doc, onlySelection:false, filePath: dlg.FileName, layerStepdown: false);
        if (!ok)
        {
            MessageBox.Show(this, "Export failed", MessageBoxType.Error);
        }
        else
        {
            MessageBox.Show(this, "Export successful", MessageBoxType.Information);
        }
    }
}
