using Eto.Drawing;
using Eto.Forms;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.UI;

/// <summary>
/// Dialog for CNCAddGroove (Nut) command parameters.
/// </summary>
public sealed class GrooveOperationDialog : CamOperationDialogBase
{
    private TextBox _widthTextBox;

    public GrooveOperationDialog(ToolLibraryStore toolLibraryStore, ToolLibrary toolLibrary) 
        : base(toolLibraryStore, toolLibrary, "Nut hinzufügen", ToolKind.EndMill)
    {
    }

    protected override void InitializeControls()
    {
        base.InitializeControls();

        _widthTextBox = new TextBox { Width = 80 };
    }

    protected override Control CreateParameterLayout()
    {
        return new TableLayout
        {
            Spacing = new Size(8, 8),
            Rows =
            {
                new TableRow(new Label { Text = "Breite (mm):" }, _widthTextBox),
                new TableRow(new Label { Text = "Tiefe (mm):" }, _depthTextBox),
            }
        };
    }

    protected override void LoadDefaults()
    {
        base.LoadDefaults();
        _widthTextBox.Text = "5.0";
        _depthTextBox.Text = "8.0"; // Override default depth for grooves
    }

    protected override bool ValidateInput()
    {
        if (!base.ValidateInput())
            return false;

        if (!double.TryParse(_widthTextBox.Text, out var width) || width <= 0)
        {
            ShowError("Bitte geben Sie eine gültige Breite ein (größer als 0).");
            _widthTextBox.Focus();
            return false;
        }

        return true;
    }

    protected override Dictionary<string, object> CreateParameters()
    {
        var selectedTool = GetSelectedTool();
        var parameters = new Dictionary<string, object>
        {
            [CncOperationSchema.CNC_TOOL] = selectedTool?.Name ?? "",
            [CncOperationSchema.CNC_WIDTH] = double.Parse(_widthTextBox.Text),
            [CncOperationSchema.CNC_DEPTH] = double.Parse(_depthTextBox.Text),
        };

        return parameters;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, "Fehler", MessageBoxType.Error);
    }
}