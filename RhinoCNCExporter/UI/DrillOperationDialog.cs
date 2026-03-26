using Eto.Drawing;
using Eto.Forms;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.UI;

/// <summary>
/// Dialog for CNCAddDrill command parameters.
/// </summary>
public sealed class DrillOperationDialog : CamOperationDialogBase
{
    private TextBox _diameterTextBox;
    private CheckBox _peckDrillingCheckBox;
    private TextBox _peckDepthTextBox;

    public DrillOperationDialog(ToolLibraryStore toolLibraryStore, ToolLibrary toolLibrary) 
        : base(toolLibraryStore, toolLibrary, "Bohrung hinzufügen", ToolKind.Drill)
    {
    }

    protected override void InitializeControls()
    {
        base.InitializeControls();

        _diameterTextBox = new TextBox { Width = 80 };
        _peckDrillingCheckBox = new CheckBox { Text = "Tieflochbohren" };
        _peckDepthTextBox = new TextBox { Width = 80, Enabled = false };
    }

    protected override Control CreateParameterLayout()
    {
        return new TableLayout
        {
            Spacing = new Size(8, 8),
            Rows =
            {
                new TableRow(new Label { Text = "Durchmesser (mm):" }, _diameterTextBox),
                new TableRow(new Label { Text = "Tiefe (mm):" }, _depthTextBox),
                new TableRow(_peckDrillingCheckBox, null),
                new TableRow(new Label { Text = "Zustell-Tiefe (mm):" }, _peckDepthTextBox),
            }
        };
    }

    protected override void SetupEventHandlers()
    {
        base.SetupEventHandlers();
        _peckDrillingCheckBox.CheckedChanged += OnPeckDrillingChanged;
        _diameterTextBox.TextChanged += OnDiameterChanged;
    }

    protected override void LoadDefaults()
    {
        base.LoadDefaults();
        _diameterTextBox.Text = "5.0";
        _peckDepthTextBox.Text = "3.0";
        
        // Auto-select diameter if we have a matching tool
        UpdateToolSelection();
    }

    public override void PreFill(MachiningOperation operation)
    {
        base.PreFill(operation);
        if (operation.Diameter.HasValue)
            _diameterTextBox.Text = operation.Diameter.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        if (operation.Peck == true)
        {
            _peckDrillingCheckBox.Checked = true;
            _peckDepthTextBox.Enabled = true;
        }
        if (operation.PeckDepth.HasValue)
            _peckDepthTextBox.Text = operation.PeckDepth.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void OnPeckDrillingChanged(object? sender, EventArgs e)
    {
        _peckDepthTextBox.Enabled = _peckDrillingCheckBox.Checked ?? false;
    }

    private void OnDiameterChanged(object? sender, EventArgs e)
    {
        UpdateToolSelection();
    }

    private void UpdateToolSelection()
    {
        if (double.TryParse(_diameterTextBox.Text, out var diameter))
        {
            // Try to find a tool with matching diameter
            var matchingToolIndex = _availableTools
                .FindIndex(t => Math.Abs(t.NominalDiameter - diameter) < 0.1);
            
            if (matchingToolIndex >= 0)
            {
                _toolDropDown.SelectedIndex = matchingToolIndex;
            }
        }
    }

    protected override bool ValidateInput()
    {
        if (!base.ValidateInput())
            return false;

        if (!double.TryParse(_diameterTextBox.Text, out var diameter) || diameter <= 0)
        {
            ShowError("Bitte geben Sie einen gültigen Durchmesser ein (größer als 0).");
            _diameterTextBox.Focus();
            return false;
        }

        if (_peckDrillingCheckBox.Checked == true)
        {
            if (!double.TryParse(_peckDepthTextBox.Text, out var peckDepth) || peckDepth <= 0)
            {
                ShowError("Bitte geben Sie eine gültige Zustell-Tiefe ein (größer als 0).");
                _peckDepthTextBox.Focus();
                return false;
            }

            var totalDepth = double.Parse(_depthTextBox.Text);
            if (peckDepth >= totalDepth)
            {
                ShowError("Die Zustell-Tiefe muss kleiner als die Gesamttiefe sein.");
                _peckDepthTextBox.Focus();
                return false;
            }
        }

        return true;
    }

    protected override Dictionary<string, object> CreateParameters()
    {
        var parameters = new Dictionary<string, object>
        {
            [CncOperationSchema.CNC_DIAMETER] = double.Parse(_diameterTextBox.Text),
            [CncOperationSchema.CNC_DEPTH] = double.Parse(_depthTextBox.Text),
        };

        if (_peckDrillingCheckBox.Checked == true)
        {
            parameters[CncOperationSchema.CNC_PECK] = true;
            parameters[CncOperationSchema.CNC_PECK_DEPTH] = double.Parse(_peckDepthTextBox.Text);
        }

        return parameters;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, "Fehler", MessageBoxType.Error);
    }
}