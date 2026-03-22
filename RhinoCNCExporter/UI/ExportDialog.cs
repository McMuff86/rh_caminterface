using System;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoCNCExporter.UI;

public sealed class ExportDialog : Dialog<bool>
{
    private readonly RadioButton zTechStepdown;
    private readonly RadioButton zLayerStepdown;
    private readonly CheckBox onlySelection;

    /// <summary>True = layer-defined stepdown (_Sxx), false = technology stepdown.</summary>
    public bool UseLayerStepdown => zLayerStepdown.Checked;

    /// <summary>True = export only selected objects.</summary>
    public bool OnlySelected => onlySelection.Checked ?? false;

    public ExportDialog()
    {
        Title = "Export Xilog";
        Resizable = false;
        Padding = new Padding(12);

        zTechStepdown = new RadioButton { Text = "A: Technologie-Stepdown (Standard)", Checked = true };
        zLayerStepdown = new RadioButton(zTechStepdown) { Text = "B: Layer-Stepdown (_Sxx)" };
        onlySelection = new CheckBox { Text = "Nur selektierte Geometrie", Checked = false };

        var exportButton = new Button { Text = "Export" };
        exportButton.Click += (_, _) => { Result = true; Close(); };

        var cancelButton = new Button { Text = "Abbrechen" };
        cancelButton.Click += (_, _) => { Result = false; Close(); };

        Content = new StackLayout
        {
            Spacing = 8,
            Items =
            {
                new Label { Text = "Zustellstrategie wählen:", Font = new Font(SystemFont.Bold, 11) },
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
