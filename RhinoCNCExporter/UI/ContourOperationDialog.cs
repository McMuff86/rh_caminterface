using Eto.Drawing;
using Eto.Forms;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.UI;

/// <summary>
/// Dialog for CNCAddContour command parameters.
/// </summary>
public sealed class ContourOperationDialog : CamOperationDialogBase
{
    private DropDown _operationTypeDropDown = null!;
    private DropDown _strategyDropDown = null!;
    private TextBox _feedrateTextBox = null!;

    public ContourOperationDialog(ToolLibraryStore toolLibraryStore, ToolLibrary toolLibrary) 
        : base(toolLibraryStore, toolLibrary, "Kontur-Bearbeitung hinzufügen", ToolKind.Router)
    {
    }

    protected override void InitializeControls()
    {
        base.InitializeControls();

        _operationTypeDropDown = new DropDown
        {
            Items = { "Konturfräsen", "Schruppen", "Schlichten" },
            SelectedIndex = 0
        };

        _strategyDropDown = new DropDown
        {
            Items = { "Gleichlauf (climb)", "Gegenlauf (conventional)" },
            SelectedIndex = 0 // Default to climb milling
        };

        _feedrateTextBox = new TextBox { Width = 80 };
    }

    protected override Control CreateParameterLayout()
    {
        return new TableLayout
        {
            Spacing = new Size(8, 8),
            Rows =
            {
                new TableRow(new Label { Text = "Bearbeitungstyp:" }, _operationTypeDropDown),
                new TableRow(new Label { Text = "Tiefe (mm):" }, _depthTextBox),
                new TableRow(new Label { Text = "Strategie:" }, _strategyDropDown),
                new TableRow(new Label { Text = "Vorschub (mm/min):" }, _feedrateTextBox),
            }
        };
    }

    protected override void LoadDefaults()
    {
        base.LoadDefaults();
        _feedrateTextBox.Text = "3000";
    }

    public override void PreFill(MachiningOperation operation)
    {
        base.PreFill(operation);
        if (operation.Strategy != null)
        {
            _operationTypeDropDown.SelectedIndex = operation.Strategy.ToUpperInvariant() switch
            {
                "ROUGH" => 1,
                "FINISH" => 2,
                _ => 0 // Both → Konturfräsen
            };
        }
        if (operation.Feedrate.HasValue)
            _feedrateTextBox.Text = operation.Feedrate.Value.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
    }

    protected override bool ValidateInput()
    {
        if (!base.ValidateInput())
            return false;

        if (!string.IsNullOrEmpty(_feedrateTextBox.Text))
        {
            if (!double.TryParse(_feedrateTextBox.Text, out var feedrate) || feedrate <= 0)
            {
                ShowError("Bitte geben Sie einen gültigen Vorschub ein (größer als 0).");
                _feedrateTextBox.Focus();
                return false;
            }
        }

        return true;
    }

    protected override Dictionary<string, object> CreateParameters()
    {
        var selectedTool = GetSelectedTool();
        var parameters = new Dictionary<string, object>
        {
            [CncOperationSchema.CNC_TOOL] = selectedTool?.Name ?? "",
            [CncOperationSchema.CNC_DEPTH] = double.Parse(_depthTextBox.Text),
        };

        // Map operation type to strategy
        var strategy = _operationTypeDropDown.SelectedIndex switch
        {
            0 => CncOperationSchema.STRATEGY_BOTH, // Konturfräsen
            1 => CncOperationSchema.STRATEGY_ROUGH, // Schruppen  
            2 => CncOperationSchema.STRATEGY_FINISH, // Schlichten
            _ => CncOperationSchema.STRATEGY_BOTH
        };
        parameters[CncOperationSchema.CNC_STRATEGY] = strategy;

        // Add feedrate if specified
        if (!string.IsNullOrEmpty(_feedrateTextBox.Text) && 
            double.TryParse(_feedrateTextBox.Text, out var feedrate))
        {
            parameters[CncOperationSchema.CNC_FEEDRATE] = feedrate;
        }

        return parameters;
    }

}