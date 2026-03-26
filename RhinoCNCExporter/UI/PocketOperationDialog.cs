using Eto.Drawing;
using Eto.Forms;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.UI;

/// <summary>
/// Dialog for CNCAddPocket command parameters.
/// </summary>
public sealed class PocketOperationDialog : CamOperationDialogBase
{
    private TextBox _stepoverTextBox;
    private DropDown _strategyDropDown;
    private DropDown _rampEntryDropDown;

    public PocketOperationDialog(ToolLibraryStore toolLibraryStore, ToolLibrary toolLibrary) 
        : base(toolLibraryStore, toolLibrary, "Tasche hinzufügen", ToolKind.Router)
    {
    }

    protected override void InitializeControls()
    {
        base.InitializeControls();

        _stepoverTextBox = new TextBox { Width = 80 };

        _strategyDropDown = new DropDown
        {
            Items = { "Schruppen", "Schlichten", "Schruppen + Schlichten" },
            SelectedIndex = 2 // Default to both rough and finish
        };

        _rampEntryDropDown = new DropDown
        {
            Items = { "Gerade", "Spirale", "Profil" },
            SelectedIndex = 1 // Default to spiral ramp
        };
    }

    protected override Control CreateParameterLayout()
    {
        return new TableLayout
        {
            Spacing = new Size(8, 8),
            Rows =
            {
                new TableRow(new Label { Text = "Tiefe (mm):" }, _depthTextBox),
                new TableRow(new Label { Text = "Stepover (%):" }, _stepoverTextBox),
                new TableRow(new Label { Text = "Strategie:" }, _strategyDropDown),
                new TableRow(new Label { Text = "Eintauchen:" }, _rampEntryDropDown),
            }
        };
    }

    protected override void LoadDefaults()
    {
        base.LoadDefaults();
        _stepoverTextBox.Text = "60";
    }

    protected override bool ValidateInput()
    {
        if (!base.ValidateInput())
            return false;

        if (!double.TryParse(_stepoverTextBox.Text, out var stepover) || stepover <= 0 || stepover > 100)
        {
            ShowError("Bitte geben Sie einen gültigen Stepover ein (1-100%).");
            _stepoverTextBox.Focus();
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
            [CncOperationSchema.CNC_DEPTH] = double.Parse(_depthTextBox.Text),
            [CncOperationSchema.CNC_STEPOVER] = double.Parse(_stepoverTextBox.Text),
        };

        // Map strategy
        var strategy = _strategyDropDown.SelectedIndex switch
        {
            0 => CncOperationSchema.STRATEGY_ROUGH,
            1 => CncOperationSchema.STRATEGY_FINISH,
            2 => CncOperationSchema.STRATEGY_BOTH,
            _ => CncOperationSchema.STRATEGY_BOTH
        };
        parameters[CncOperationSchema.CNC_STRATEGY] = strategy;

        // Map ramp entry
        var rampEntry = _rampEntryDropDown.SelectedIndex switch
        {
            0 => CncOperationSchema.RAMP_STRAIGHT,
            1 => CncOperationSchema.RAMP_SPIRAL,
            2 => CncOperationSchema.RAMP_PROFILE,
            _ => CncOperationSchema.RAMP_SPIRAL
        };
        parameters[CncOperationSchema.CNC_RAMP_ENTRY] = rampEntry;

        return parameters;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, "Fehler", MessageBoxType.Error);
    }
}