using Eto.Drawing;
using Eto.Forms;
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

    protected DropDown _toolDropDown;
    protected TextBox _depthTextBox;
    protected Label _toolInfoLabel;
    protected Button _okButton;
    protected Button _cancelButton;

    protected CamOperationDialogBase(ToolLibraryStore toolLibraryStore, ToolLibrary toolLibrary, string title, ToolKind toolKind)
    {
        _toolLibraryStore = toolLibraryStore ?? throw new ArgumentNullException(nameof(toolLibraryStore));
        _toolLibrary = toolLibrary ?? throw new ArgumentNullException(nameof(toolLibrary));

        Title = title;
        ClientSize = new Size(400, 280);
        Padding = new Padding(16);
        Resizable = false;

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
        var selectedTool = GetSelectedTool();
        if (selectedTool != null)
        {
            _toolInfoLabel.Text = $"⌀{selectedTool.NominalDiameter:F1}mm, L{selectedTool.CuttingLength:F0}mm";
        }
        else
        {
            _toolInfoLabel.Text = "";
        }
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
            _toolDropDown.Items.Add($"{tool.Name} (⌀{tool.NominalDiameter:F1}mm)");
        }
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

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, "Fehler", MessageBoxType.Error);
    }
}