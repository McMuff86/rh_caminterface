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
    private TextBox _stepoverTextBox = null!;
    private DropDown _strategyDropDown = null!;
    private DropDown _rampEntryDropDown = null!;

    public PocketOperationDialog(ToolLibraryStore toolLibraryStore, ToolLibrary toolLibrary) 
        : base(toolLibraryStore, toolLibrary, "Tasche hinzufügen", ToolKind.Router)
    {
    }

    /// <summary>
    /// Creates a pocket dialog with machine-profile defaults pre-filled.
    /// </summary>
    public PocketOperationDialog(ToolLibraryStore toolLibraryStore, ToolLibrary toolLibrary, OperationDefaultValues defaults)
        : base(toolLibraryStore, toolLibrary, "Tasche hinzufügen", ToolKind.Router, defaults)
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
        var stepover = _operationDefaults?.Stepover ?? 60.0;
        _stepoverTextBox.Text = stepover.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);

        // Map strategy default
        if (_operationDefaults?.Strategy != null)
        {
            _strategyDropDown.SelectedIndex = _operationDefaults.Strategy.ToUpperInvariant() switch
            {
                "ROUGH" => 0,
                "FINISH" => 1,
                _ => 2 // Both
            };
        }

        // Map ramp entry default
        if (_operationDefaults?.RampEntry != null)
        {
            _rampEntryDropDown.SelectedIndex = _operationDefaults.RampEntry.ToUpperInvariant() switch
            {
                "STRAIGHT" => 0,
                "SPIRAL" => 1,
                "PROFILE" => 2,
                _ => 1
            };
        }
    }

    public override void PreFill(MachiningOperation operation)
    {
        base.PreFill(operation);
        if (operation.Stepover.HasValue)
            _stepoverTextBox.Text = operation.Stepover.Value.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
        if (operation.Strategy != null)
        {
            _strategyDropDown.SelectedIndex = operation.Strategy.ToUpperInvariant() switch
            {
                "ROUGH" => 0,
                "FINISH" => 1,
                _ => 2 // Both
            };
        }
        if (operation.RampEntry != null)
        {
            _rampEntryDropDown.SelectedIndex = operation.RampEntry.ToUpperInvariant() switch
            {
                "STRAIGHT" => 0,
                "SPIRAL" => 1,
                "PROFILE" => 2,
                _ => 1
            };
        }
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

}