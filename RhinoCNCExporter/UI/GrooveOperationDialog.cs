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
    private TextBox _widthTextBox = null!;

    public GrooveOperationDialog(ToolLibraryStore toolLibraryStore, ToolLibrary toolLibrary) 
        : base(toolLibraryStore, toolLibrary, "Nut hinzufügen", ToolKind.Router)
    {
    }

    /// <summary>
    /// Creates a groove dialog with machine-profile defaults pre-filled.
    /// </summary>
    public GrooveOperationDialog(ToolLibraryStore toolLibraryStore, ToolLibrary toolLibrary, OperationDefaultValues defaults)
        : base(toolLibraryStore, toolLibrary, "Nut hinzufügen", ToolKind.Router, defaults)
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
        var width = _operationDefaults?.Width ?? 5.0;
        _widthTextBox.Text = width.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        // Depth is already set by base.LoadDefaults() from _operationDefaults
    }

    public override void PreFill(MachiningOperation operation)
    {
        base.PreFill(operation);
        if (operation.Width.HasValue)
            _widthTextBox.Text = operation.Width.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
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

}