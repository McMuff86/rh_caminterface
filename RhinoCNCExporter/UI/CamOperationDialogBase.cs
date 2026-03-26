using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.UI;

/// <summary>
/// Base class for CNC operation dialogs with common UI patterns and tool selection.
/// </summary>
public abstract class CamOperationDialogBase : Dialog<Dictionary<string, object>?>
{
    protected readonly ToolLibraryStore _toolLibraryStore;
    protected readonly ToolLibrary _toolLibrary;
    protected readonly List<ToolDefinition> _availableTools;

    protected DropDown _toolDropDown = null!;
    protected TextBox _depthTextBox = null!;
    protected Label _toolInfoLabel = null!;
    protected Button _okButton = null!;
    protected Button _cancelButton = null!;

    protected CamOperationDialogBase(ToolLibraryStore toolLibraryStore, ToolLibrary toolLibrary, string title, ToolKind toolKind)
    {
        _toolLibraryStore = toolLibraryStore ?? throw new ArgumentNullException(nameof(toolLibraryStore));
        _toolLibrary = toolLibrary ?? throw new ArgumentNullException(nameof(toolLibrary));

        Title = title;
        ClientSize = new Size(400, 380);
        Padding = new Padding(16);
        Resizable = true;

        // Filter tools by type
        _availableTools = toolLibrary.Tools
            .Where(t => t.Kind == toolKind)
            .OrderBy(t => t.NominalDiameter)
            .ThenBy(t => t.Name)
            .ToList();

        InitializeControls();
        SetupLayout();
        SetupEventHandlers();
        LoadDefaults();
    }

    protected virtual void InitializeControls()
    {
        _toolDropDown = new DropDown();
        _depthTextBox = new TextBox { Width = 80 };
        _toolInfoLabel = new Label { Text = "", Font = new Font(SystemFont.Default, 9) };
        
        _okButton = new Button { Text = "OK", Width = 80 };
        _cancelButton = new Button { Text = "Abbrechen", Width = 80 };

        PopulateToolDropDown();
    }

    protected virtual void SetupLayout()
    {
        var toolSection = CreateSection("Werkzeug", new TableLayout
        {
            Spacing = new Size(8, 8),
            Rows =
            {
                new TableRow(new Label { Text = "Werkzeug:" }, _toolDropDown),
                new TableRow(new Label(), _toolInfoLabel),
            }
        });

        var parametersSection = CreateSection("Parameter", CreateParameterLayout());

        var buttonLayout = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Items = { null, _okButton, _cancelButton }
        };

        Content = new StackLayout
        {
            Spacing = 16,
            Items =
            {
                toolSection,
                parametersSection,
                buttonLayout
            }
        };
    }

    protected virtual Control CreateParameterLayout()
    {
        return new TableLayout
        {
            Spacing = new Size(8, 8),
            Rows =
            {
                new TableRow(new Label { Text = "Tiefe (mm):" }, _depthTextBox),
            }
        };
    }

    protected virtual void SetupEventHandlers()
    {
        _toolDropDown.SelectedIndexChanged += OnToolChanged;
        _okButton.Click += OnOkClick;
        _cancelButton.Click += OnCancelClick;
    }

    protected virtual void LoadDefaults()
    {
        if (_availableTools.Count > 0)
        {
            _toolDropDown.SelectedIndex = 0;
        }
        _depthTextBox.Text = "10.0";
    }

    protected virtual void OnToolChanged(object? sender, EventArgs e)
    {
        // Check if "Manage Tools" was selected (last item)
        if (_toolDropDown.SelectedIndex >= 0 &&
            _toolDropDown.SelectedIndex == _toolDropDown.Items.Count - 1)
        {
            // Reset selection to previous valid tool
            _toolDropDown.SelectedIndex = _availableTools.Count > 0 ? 0 : -1;

            // Open tool library manager
            try
            {
                var dialog = new ToolLibraryManagerDialog(_toolLibrary);
                var result = dialog.ShowModal(this);
                if (result != null)
                {
                    // Note: tool library changes will be picked up on next dialog open
                    RhinoApp.WriteLine($"[CamDialog] Werkzeugbibliothek bearbeitet: {result.Tools.Count} Werkzeuge");
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[CamDialog] Werkzeugmanager-Fehler: {ex.Message}");
            }
            return;
        }

        var selectedTool = GetSelectedTool();
        if (selectedTool != null)
        {
            _toolInfoLabel.Text = $"Ø{selectedTool.NominalDiameter:F1}mm, L{selectedTool.CuttingLength:F0}mm";
        }
        else
        {
            _toolInfoLabel.Text = "";
        }
    }

    /// <summary>
    /// Show this dialog as modal with Rhino as owner so it stays on top.
    /// </summary>
    public Dictionary<string, object>? ShowModalOnTop()
    {
        return ShowModal(RhinoEtoApp.MainWindow);
    }

    /// <summary>
    /// Pre-fill the dialog with values from an existing operation (for editing).
    /// Override in subclasses to handle type-specific parameters.
    /// </summary>
    public virtual void PreFill(MachiningOperation operation)
    {
        if (operation == null) return;

        // Select tool by name
        if (!string.IsNullOrEmpty(operation.Tool))
        {
            for (int i = 0; i < _availableTools.Count; i++)
            {
                if (_availableTools[i].Name.Equals(operation.Tool, StringComparison.OrdinalIgnoreCase))
                {
                    _toolDropDown.SelectedIndex = i;
                    break;
                }
            }
        }

        // Set depth
        if (operation.Depth.HasValue)
            _depthTextBox.Text = operation.Depth.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }

    protected virtual void OnOkClick(object? sender, EventArgs e)
    {
        if (!ValidateInput())
            return;

        Result = CreateParameters();
        Close();
    }

    protected virtual void OnCancelClick(object? sender, EventArgs e)
    {
        Result = null;
        Close();
    }

    protected virtual bool ValidateInput()
    {
        if (_toolDropDown.SelectedIndex < 0)
        {
            ShowError("Bitte wählen Sie ein Werkzeug aus.");
            return false;
        }

        if (!double.TryParse(_depthTextBox.Text, out var depth) || depth <= 0)
        {
            ShowError("Bitte geben Sie eine gültige Tiefe ein (größer als 0).");
            _depthTextBox.Focus();
            return false;
        }

        return true;
    }

    protected abstract Dictionary<string, object> CreateParameters();

    protected ToolDefinition? GetSelectedTool()
    {
        var index = _toolDropDown.SelectedIndex;
        return index >= 0 && index < _availableTools.Count ? _availableTools[index] : null;
    }

    private void PopulateToolDropDown()
    {
        _toolDropDown.Items.Clear();
        foreach (var tool in _availableTools)
        {
            var fluteInfo = tool.FluteCount.HasValue ? $" ({tool.FluteCount}-Schneider)" : "";
            _toolDropDown.Items.Add($"Ø{tool.NominalDiameter:F1} {tool.Name}{fluteInfo}");
        }

        if (_availableTools.Count == 0)
        {
            _toolDropDown.Items.Add("⚠ Keine Werkzeuge verfügbar");
        }

        // "Manage Tools..." separator item
        _toolDropDown.Items.Add("── Werkzeuge verwalten… ──");
    }

    private Control CreateSection(string title, Control content)
    {
        return new GroupBox
        {
            Text = title,
            Content = new StackLayout
            {
                Padding = new Padding(8),
                Items = { content }
            }
        };
    }

    protected void ShowError(string message)
    {
        MessageBox.Show(this, message, "Fehler", MessageBoxType.Error);
    }
}