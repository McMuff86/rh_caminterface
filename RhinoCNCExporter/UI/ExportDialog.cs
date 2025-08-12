using System;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoCNCExporter.UI;

public sealed class ExportDialog : Dialog<bool>
{
    private readonly RadioButton zTechStepdown;
    private readonly RadioButton zLayerStepdown;
    private readonly CheckBox onlySelection;
    private readonly Button exportButton;

    public ExportDialog()
    {
        Title = "Export Xilog";
        Resizable = false;
        Padding = new Padding(12);

        zTechStepdown = new RadioButton { Text = "Z strategy: Technology stepdown", Checked = true };
        zLayerStepdown = new RadioButton(zTechStepdown) { Text = "Z strategy: Layer stepdown" };
        onlySelection = new CheckBox { Text = "Only selected geometry", Checked = false };

        exportButton = new Button { Text = "Export" };
        exportButton.Click += (_, _) => { Result = true; Close(); };

        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Click += (_, _) => { Result = false; Close(); };

        Content = new StackLayout
        {
            Spacing = 8,
            Items =
            {
                new Label { Text = "Choose options:", Font = new Font(SystemFont.Bold, 11) },
                zTechStepdown,
                zLayerStepdown,
                onlySelection,
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Items = { exportButton, cancelButton }
                }
            }
        };
    }
}
