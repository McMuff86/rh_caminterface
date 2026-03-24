using System.Globalization;
using Eto.Drawing;
using Eto.Forms;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.UI;

public sealed class ToolLibraryManagerDialog : Dialog<ToolLibrary?>
{
    private const int ToolGridContentWidth = 860;
    private const int HolderGridContentWidth = 780;

    private readonly Label _summaryLabel;
    private readonly GridView _toolGrid;
    private readonly GridView _holderGrid;
    private readonly ToolAssemblyPreview _toolPreview;
    private readonly ToolAssemblyPreview _holderPreview;
    private readonly Label _toolPreviewSummaryLabel;
    private readonly Label _holderPreviewSummaryLabel;

    private readonly TextBox _toolIdTextBox;
    private readonly TextBox _toolNameTextBox;
    private readonly DropDown _toolKindDropDown;
    private readonly DropDown _toolHolderDropDown;
    private readonly TextBox _toolTechCodeTextBox;
    private readonly TextBox _toolDiameterTextBox;
    private readonly TextBox _toolShankDiameterTextBox;
    private readonly TextBox _toolCornerRadiusTextBox;
    private readonly TextBox _toolCuttingLengthTextBox;
    private readonly TextBox _toolOverallLengthTextBox;
    private readonly TextBox _toolFluteCountTextBox;
    private readonly DropDown _toolMaterialDropDown;
    private readonly TextBox _toolSpindleTextBox;
    private readonly TextBox _toolFeedTextBox;
    private readonly TextBox _toolPlungeFeedTextBox;
    private readonly TextBox _toolStepDownTextBox;
    private readonly TextBox _toolStepOverTextBox;
    private readonly TextArea _toolDescriptionTextArea;

    private readonly TextBox _holderIdTextBox;
    private readonly TextBox _holderNameTextBox;
    private readonly DropDown _holderKindDropDown;
    private readonly TextBox _holderGaugeLengthTextBox;
    private readonly TextBox _holderGaugeDiameterTextBox;
    private readonly TextBox _holderProjectionTextBox;
    private readonly TextArea _holderDescriptionTextArea;

    private ToolLibrary _library;
    private List<ToolDefinition> _toolRows = new();
    private List<ToolHolderDefinition> _holderRows = new();
    private List<ToolHolderDefinition> _holderChoices = new();

    public ToolLibraryManagerDialog(ToolLibrary library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));

        Title = $"Werkzeugmanager - {library.Name}";
        ClientSize = new Size(1320, 820);
        MinimumSize = new Size(1080, 720);
        Padding = new Padding(12);
        Resizable = true;

        _summaryLabel = new Label
        {
            Text = string.Empty,
            Font = new Font(SystemFont.Bold, 11)
        };

        _toolGrid = CreateToolGrid();
        _toolGrid.SelectionChanged += (_, _) => LoadSelectedTool();

        _holderGrid = CreateHolderGrid();
        _holderGrid.SelectionChanged += (_, _) => LoadSelectedHolder();

        _toolPreview = new ToolAssemblyPreview("Keine Werkzeug-Assembly ausgewählt.");
        _holderPreview = new ToolAssemblyPreview("Kein Halter ausgewählt.");
        _toolPreviewSummaryLabel = CreatePreviewSummaryLabel();
        _holderPreviewSummaryLabel = CreatePreviewSummaryLabel();

        _toolIdTextBox = CreateTextBox();
        _toolNameTextBox = CreateTextBox();
        _toolKindDropDown = CreateEnumDropDown<ToolKind>();
        _toolHolderDropDown = new DropDown();
        _toolTechCodeTextBox = CreateTextBox();
        _toolDiameterTextBox = CreateTextBox();
        _toolShankDiameterTextBox = CreateTextBox();
        _toolCornerRadiusTextBox = CreateTextBox();
        _toolCuttingLengthTextBox = CreateTextBox();
        _toolOverallLengthTextBox = CreateTextBox();
        _toolFluteCountTextBox = CreateTextBox();
        _toolMaterialDropDown = CreateEnumDropDown<ToolMaterial>();
        _toolSpindleTextBox = CreateTextBox();
        _toolFeedTextBox = CreateTextBox();
        _toolPlungeFeedTextBox = CreateTextBox();
        _toolStepDownTextBox = CreateTextBox();
        _toolStepOverTextBox = CreateTextBox();
        _toolDescriptionTextArea = CreateTextArea();

        _holderIdTextBox = CreateTextBox();
        _holderNameTextBox = CreateTextBox();
        _holderKindDropDown = CreateEnumDropDown<HolderKind>();
        _holderGaugeLengthTextBox = CreateTextBox();
        _holderGaugeDiameterTextBox = CreateTextBox();
        _holderProjectionTextBox = CreateTextBox();
        _holderDescriptionTextArea = CreateTextArea();

        WirePreviewRefresh();

        var toolsTab = new TabPage
        {
            Text = "Werkzeuge",
            Content = BuildToolPage()
        };

        var holdersTab = new TabPage
        {
            Text = "Halter",
            Content = BuildHolderPage()
        };

        var tabs = new TabControl
        {
            Pages = { toolsTab, holdersTab }
        };

        var saveButton = new Button { Text = "Speichern", Width = 110 };
        saveButton.Click += (_, _) =>
        {
            Result = _library;
            Close();
        };

        var cancelButton = new Button { Text = "Abbrechen", Width = 110 };
        cancelButton.Click += (_, _) =>
        {
            Result = null;
            Close();
        };

        DefaultButton = saveButton;
        AbortButton = cancelButton;

        Content = new StackLayout
        {
            Spacing = 10,
            Items =
            {
                _summaryLabel,
                new StackLayoutItem(tabs, true),
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalContentAlignment = HorizontalAlignment.Right,
                    Spacing = 6,
                    Items = { saveButton, cancelButton }
                }
            }
        };

        RefreshHolderChoices();
        ReloadHolderGrid();
        ReloadToolGrid();
        RefreshSummary();
    }

    private Control BuildToolPage()
    {
        var newButton = new Button { Text = "Neu", Width = 90 };
        newButton.Click += (_, _) => CreateNewTool();

        var duplicateButton = new Button { Text = "Duplizieren", Width = 100 };
        duplicateButton.Click += (_, _) => DuplicateSelectedTool();

        var deleteButton = new Button { Text = "Löschen", Width = 90 };
        deleteButton.Click += (_, _) => DeleteSelectedTool();

        var applyButton = new Button { Text = "Änderungen übernehmen", Width = 190 };
        applyButton.Click += (_, _) => ApplyToolChanges();

        var left = new StackLayout
        {
            Padding = new Padding(0, 0, 6, 0),
            Spacing = 8,
            Items =
            {
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Items = { newButton, duplicateButton, deleteButton }
                },
                CreateHintLabel("`Neu` und `Duplizieren` legen sofort Datensätze an. Formularänderungen werden mit `Änderungen übernehmen` gespeichert."),
                CreateHintLabel("Die Werkzeugliste bleibt bei schmalem Splitter horizontal und vertikal scrollbar."),
                new StackLayoutItem(CreateScrollableGridPane(_toolGrid, ToolGridContentWidth), true)
            }
        };

        var editor = new Scrollable
        {
            Border = BorderType.None,
            Content = new StackLayout
            {
                Spacing = 10,
                Padding = new Padding(0, 0, 6, 0),
                Items =
                {
                    CreateEditorSection("Stammdaten", new TableLayout
                    {
                        Spacing = new Size(10, 6),
                        Rows =
                        {
                            CreateEditorRow("Werkzeug-ID", _toolIdTextBox),
                            CreateEditorRow("Name", _toolNameTextBox),
                            CreateEditorRow("Typ", _toolKindDropDown),
                            CreateEditorRow("Halter", _toolHolderDropDown),
                            CreateEditorRow("Tech-/E-Code", _toolTechCodeTextBox),
                            CreateEditorRow("Beschreibung", _toolDescriptionTextArea)
                        }
                    }),
                    CreateEditorSection("Geometrie", new TableLayout
                    {
                        Spacing = new Size(10, 6),
                        Rows =
                        {
                            CreateEditorRow("Durchmesser (mm)", _toolDiameterTextBox),
                            CreateEditorRow("Schaft-Ø (mm)", _toolShankDiameterTextBox),
                            CreateEditorRow("Eckenradius (mm)", _toolCornerRadiusTextBox),
                            CreateEditorRow("Schneidenlänge (mm)", _toolCuttingLengthTextBox),
                            CreateEditorRow("Gesamtlänge (mm)", _toolOverallLengthTextBox),
                            CreateEditorRow("Schneiden", _toolFluteCountTextBox),
                            CreateEditorRow("Material", _toolMaterialDropDown)
                        }
                    }),
                    CreateEditorSection("Schnittparameter", new TableLayout
                    {
                        Spacing = new Size(10, 6),
                        Rows =
                        {
                            CreateEditorRow("Drehzahl (rpm)", _toolSpindleTextBox),
                            CreateEditorRow("Vorschub (mm/min)", _toolFeedTextBox),
                            CreateEditorRow("Eintauchvorschub", _toolPlungeFeedTextBox),
                            CreateEditorRow("Stepdown (mm)", _toolStepDownTextBox),
                            CreateEditorRow("Stepover (mm)", _toolStepOverTextBox)
                        }
                    }),
                    new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalContentAlignment = HorizontalAlignment.Right,
                        Items = { applyButton }
                    }
                }
            }
        };

        var preview = CreatePreviewPane(
            "Assembly-Vorschau",
            "Schematische Vorschau von Halter und Werkzeug. Formularänderungen aktualisieren die Darstellung live.",
            _toolPreview,
            _toolPreviewSummaryLabel);

        var right = new Splitter
        {
            Orientation = Orientation.Horizontal,
            Position = 500,
            Panel1 = editor,
            Panel2 = preview
        };

        return new Splitter
        {
            Orientation = Orientation.Horizontal,
            Position = 560,
            Panel1 = left,
            Panel2 = right
        };
    }

    private Control BuildHolderPage()
    {
        var newButton = new Button { Text = "Neu", Width = 90 };
        newButton.Click += (_, _) => CreateNewHolder();

        var duplicateButton = new Button { Text = "Duplizieren", Width = 100 };
        duplicateButton.Click += (_, _) => DuplicateSelectedHolder();

        var deleteButton = new Button { Text = "Löschen", Width = 90 };
        deleteButton.Click += (_, _) => DeleteSelectedHolder();

        var applyButton = new Button { Text = "Änderungen übernehmen", Width = 190 };
        applyButton.Click += (_, _) => ApplyHolderChanges();

        var left = new StackLayout
        {
            Padding = new Padding(0, 0, 6, 0),
            Spacing = 8,
            Items =
            {
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Items = { newButton, duplicateButton, deleteButton }
                },
                CreateHintLabel("Neue Halter werden sofort angelegt. Beim Löschen werden referenzierte Werkzeuge auf `kein Halter` zurückgesetzt."),
                CreateHintLabel("Die Halterliste bleibt bei schmalem Splitter horizontal und vertikal scrollbar."),
                new StackLayoutItem(CreateScrollableGridPane(_holderGrid, HolderGridContentWidth), true)
            }
        };

        var editor = new Scrollable
        {
            Border = BorderType.None,
            Content = new StackLayout
            {
                Spacing = 10,
                Padding = new Padding(0, 0, 6, 0),
                Items =
                {
                    CreateEditorSection("Halter", new TableLayout
                    {
                        Spacing = new Size(10, 6),
                        Rows =
                        {
                            CreateEditorRow("Halter-ID", _holderIdTextBox),
                            CreateEditorRow("Name", _holderNameTextBox),
                            CreateEditorRow("Typ", _holderKindDropDown),
                            CreateEditorRow("Gauge Length (mm)", _holderGaugeLengthTextBox),
                            CreateEditorRow("Gauge Ø (mm)", _holderGaugeDiameterTextBox),
                            CreateEditorRow("Auskragung (mm)", _holderProjectionTextBox),
                            CreateEditorRow("Beschreibung", _holderDescriptionTextArea)
                        }
                    }),
                    new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalContentAlignment = HorizontalAlignment.Right,
                        Items = { applyButton }
                    }
                }
            }
        };

        var preview = CreatePreviewPane(
            "Halter-Vorschau",
            "Wenn Werkzeuge mit dem Halter verknüpft sind, wird ein repräsentatives Assembly angezeigt.",
            _holderPreview,
            _holderPreviewSummaryLabel);

        var right = new Splitter
        {
            Orientation = Orientation.Horizontal,
            Position = 460,
            Panel1 = editor,
            Panel2 = preview
        };

        return new Splitter
        {
            Orientation = Orientation.Horizontal,
            Position = 560,
            Panel1 = left,
            Panel2 = right
        };
    }

    private static GridView CreateToolGrid()
    {
        var grid = new GridView();

        grid.Columns.Add(CreateTextColumn("Tech", 0, 80));
        grid.Columns.Add(CreateTextColumn("Werkzeug", 1, 210));
        grid.Columns.Add(CreateTextColumn("Typ", 2, 90));
        grid.Columns.Add(CreateTextColumn("Halter", 3, 180));
        grid.Columns.Add(CreateTextColumn("Ø", 4, 70));
        grid.Columns.Add(CreateTextColumn("L", 5, 70));
        grid.Columns.Add(CreateTextColumn("Z", 6, 50));
        grid.Columns.Add(CreateTextColumn("Vorschub", 7, 90));
        return grid;
    }

    private static GridView CreateHolderGrid()
    {
        var grid = new GridView();

        grid.Columns.Add(CreateTextColumn("Halter", 0, 250));
        grid.Columns.Add(CreateTextColumn("Typ", 1, 150));
        grid.Columns.Add(CreateTextColumn("Gauge L", 2, 90));
        grid.Columns.Add(CreateTextColumn("Gauge Ø", 3, 90));
        grid.Columns.Add(CreateTextColumn("Auskragung", 4, 100));
        grid.Columns.Add(CreateTextColumn("Werkzeuge", 5, 80));
        return grid;
    }

    private static GridColumn CreateTextColumn(string header, int index, int width)
    {
        return new GridColumn
        {
            HeaderText = header,
            Width = width,
            DataCell = new TextBoxCell(index)
        };
    }

    private static Control CreateScrollableGridPane(GridView grid, int contentWidth)
    {
        grid.Size = new Size(contentWidth, 260);

        return new Scrollable
        {
            Border = BorderType.None,
            Content = new StackLayout
            {
                Spacing = 0,
                MinimumSize = new Size(contentWidth, 260),
                Items =
                {
                    new StackLayoutItem(grid, true)
                }
            }
        };
    }

    private void WirePreviewRefresh()
    {
        foreach (var control in GetToolTextControls())
        {
            control.TextChanged += (_, _) => RefreshToolPreviewFromEditor();
        }

        _toolKindDropDown.SelectedIndexChanged += (_, _) => RefreshToolPreviewFromEditor();
        _toolHolderDropDown.SelectedIndexChanged += (_, _) => RefreshToolPreviewFromEditor();
        _toolMaterialDropDown.SelectedIndexChanged += (_, _) => RefreshToolPreviewFromEditor();

        foreach (var control in GetHolderTextControls())
        {
            control.TextChanged += (_, _) => RefreshHolderPreviewFromEditor();
        }

        _holderKindDropDown.SelectedIndexChanged += (_, _) => RefreshHolderPreviewFromEditor();
    }

    private IEnumerable<TextControl> GetToolTextControls()
    {
        yield return _toolIdTextBox;
        yield return _toolNameTextBox;
        yield return _toolTechCodeTextBox;
        yield return _toolDiameterTextBox;
        yield return _toolShankDiameterTextBox;
        yield return _toolCornerRadiusTextBox;
        yield return _toolCuttingLengthTextBox;
        yield return _toolOverallLengthTextBox;
        yield return _toolFluteCountTextBox;
        yield return _toolSpindleTextBox;
        yield return _toolFeedTextBox;
        yield return _toolPlungeFeedTextBox;
        yield return _toolStepDownTextBox;
        yield return _toolStepOverTextBox;
        yield return _toolDescriptionTextArea;
    }

    private IEnumerable<TextControl> GetHolderTextControls()
    {
        yield return _holderIdTextBox;
        yield return _holderNameTextBox;
        yield return _holderGaugeLengthTextBox;
        yield return _holderGaugeDiameterTextBox;
        yield return _holderProjectionTextBox;
        yield return _holderDescriptionTextArea;
    }

    private static Control CreatePreviewPane(string title, string hint, Control preview, Label summary)
    {
        return new StackLayout
        {
            Padding = new Padding(6, 0, 0, 0),
            Spacing = 8,
            Items =
            {
                CreateHintLabel(hint),
                CreateEditorSection(title, new StackLayout
                {
                    Spacing = 8,
                    Items =
                    {
                        new StackLayoutItem(preview, true),
                        summary
                    }
                })
            }
        };
    }

    private void ReloadToolGrid(string? selectToolId = null)
    {
        _toolRows = _library.Tools
            .OrderBy(static t => t.Kind)
            .ThenBy(static t => t.NominalDiameter)
            .ThenBy(static t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _toolGrid.DataStore = _toolRows
            .Select(tool => (object?)new object?[]
            {
                tool.TechCode ?? string.Empty,
                tool.Name,
                tool.Kind.ToString(),
                _library.FindHolderById(tool.HolderId)?.Name ?? "—",
                FormatDouble(tool.NominalDiameter),
                FormatDouble(tool.CuttingLength),
                tool.FluteCount?.ToString(CultureInfo.InvariantCulture) ?? "—",
                FormatDouble(tool.FeedRate)
            })
            .ToList();

        if (_toolRows.Count == 0)
        {
            PopulateToolEditor(CreateDraftTool());
            return;
        }

        var index = selectToolId == null
            ? 0
            : _toolRows.FindIndex(tool => string.Equals(tool.Id, selectToolId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            index = 0;

        _toolGrid.SelectedRow = index;
        PopulateToolEditor(_toolRows[index]);
    }

    private void ReloadHolderGrid(string? selectHolderId = null)
    {
        _holderRows = _library.Holders
            .OrderBy(static h => h.Kind)
            .ThenBy(static h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _holderGrid.DataStore = _holderRows
            .Select(holder => (object?)new object?[]
            {
                holder.Name,
                holder.Kind.ToString(),
                FormatDouble(holder.GaugeLength),
                FormatDouble(holder.GaugeDiameter),
                FormatDouble(holder.ProjectionLength),
                _library.Tools.Count(tool => string.Equals(tool.HolderId, holder.Id, StringComparison.OrdinalIgnoreCase))
                    .ToString(CultureInfo.InvariantCulture)
            })
            .ToList();

        if (_holderRows.Count == 0)
        {
            PopulateHolderEditor(CreateDraftHolder());
            return;
        }

        var index = selectHolderId == null
            ? 0
            : _holderRows.FindIndex(holder => string.Equals(holder.Id, selectHolderId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            index = 0;

        _holderGrid.SelectedRow = index;
        PopulateHolderEditor(_holderRows[index]);
    }

    private void RefreshHolderChoices(string? selectedHolderId = null)
    {
        _holderChoices = _library.Holders
            .OrderBy(static h => h.Kind)
            .ThenBy(static h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _toolHolderDropDown.Items.Clear();
        _toolHolderDropDown.Items.Add("(kein Halter)");
        foreach (var holder in _holderChoices)
        {
            _toolHolderDropDown.Items.Add($"{holder.Name} [{holder.Kind}]");
        }

        if (string.IsNullOrWhiteSpace(selectedHolderId))
        {
            _toolHolderDropDown.SelectedIndex = 0;
            return;
        }

        var index = _holderChoices.FindIndex(holder =>
            string.Equals(holder.Id, selectedHolderId, StringComparison.OrdinalIgnoreCase));
        _toolHolderDropDown.SelectedIndex = index >= 0 ? index + 1 : 0;
    }

    private void RefreshSummary()
    {
        _summaryLabel.Text =
            $"{_library.Name} · Maschine: {_library.MachineKey} · {_library.Tools.Count} Werkzeuge · {_library.Holders.Count} Halter";
    }

    private void RefreshLibraryAfterMutation(string? selectToolId, string? selectHolderId)
    {
        RefreshHolderChoices();
        ReloadHolderGrid(selectHolderId);
        ReloadToolGrid(selectToolId);
        RefreshSummary();
    }

    private void LoadSelectedTool()
    {
        var index = _toolGrid.SelectedRow;
        if (index >= 0 && index < _toolRows.Count)
        {
            PopulateToolEditor(_toolRows[index]);
        }
    }

    private void LoadSelectedHolder()
    {
        var index = _holderGrid.SelectedRow;
        if (index >= 0 && index < _holderRows.Count)
        {
            PopulateHolderEditor(_holderRows[index]);
        }
    }

    private void PopulateToolEditor(ToolDefinition tool)
    {
        _toolIdTextBox.Text = tool.Id;
        _toolNameTextBox.Text = tool.Name;
        SetEnumDropDownValue(_toolKindDropDown, tool.Kind);
        RefreshHolderChoices(tool.HolderId);
        _toolTechCodeTextBox.Text = tool.TechCode ?? string.Empty;
        _toolDiameterTextBox.Text = FormatDouble(tool.NominalDiameter);
        _toolShankDiameterTextBox.Text = FormatDouble(tool.ShankDiameter);
        _toolCornerRadiusTextBox.Text = FormatDouble(tool.CornerRadius);
        _toolCuttingLengthTextBox.Text = FormatDouble(tool.CuttingLength);
        _toolOverallLengthTextBox.Text = FormatDouble(tool.OverallLength);
        _toolFluteCountTextBox.Text = tool.FluteCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SetEnumDropDownValue(_toolMaterialDropDown, tool.Material);
        _toolSpindleTextBox.Text = FormatDouble(tool.SpindleSpeed);
        _toolFeedTextBox.Text = FormatDouble(tool.FeedRate);
        _toolPlungeFeedTextBox.Text = FormatDouble(tool.PlungeFeedRate);
        _toolStepDownTextBox.Text = FormatDouble(tool.DefaultStepDown);
        _toolStepOverTextBox.Text = FormatDouble(tool.DefaultStepOver);
        _toolDescriptionTextArea.Text = tool.Description ?? string.Empty;
        RefreshToolPreviewFromEditor();
    }

    private void PopulateHolderEditor(ToolHolderDefinition holder)
    {
        _holderIdTextBox.Text = holder.Id;
        _holderNameTextBox.Text = holder.Name;
        SetEnumDropDownValue(_holderKindDropDown, holder.Kind);
        _holderGaugeLengthTextBox.Text = FormatDouble(holder.GaugeLength);
        _holderGaugeDiameterTextBox.Text = FormatDouble(holder.GaugeDiameter);
        _holderProjectionTextBox.Text = FormatDouble(holder.ProjectionLength);
        _holderDescriptionTextArea.Text = holder.Description ?? string.Empty;
        RefreshHolderPreviewFromEditor();
    }

    private void RefreshToolPreviewFromEditor()
    {
        var tool = CreateDraftTool();
        var holder = ResolveHolderForPreview();
        _toolPreview.UpdatePreview(tool, holder);
        _toolPreviewSummaryLabel.Text = BuildToolPreviewSummary(tool, holder);
    }

    private void RefreshHolderPreviewFromEditor()
    {
        var holder = CreateDraftHolder();
        var representativeTool = GetRepresentativeTool(holder.Id);
        _holderPreview.UpdatePreview(representativeTool, holder);
        _holderPreviewSummaryLabel.Text = BuildHolderPreviewSummary(holder, representativeTool);
    }

    private ToolHolderDefinition? ResolveHolderForPreview()
    {
        var holderId = GetSelectedHolderId();
        return string.IsNullOrWhiteSpace(holderId)
            ? null
            : _library.FindHolderById(holderId);
    }

    private ToolDefinition? GetRepresentativeTool(string? holderId)
    {
        if (string.IsNullOrWhiteSpace(holderId))
            return null;

        return _library.Tools
            .Where(tool => string.Equals(tool.HolderId, holderId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tool => tool.Kind)
            .ThenBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string BuildToolPreviewSummary(ToolDefinition tool, ToolHolderDefinition? holder)
    {
        var lines = new List<string>
        {
            $"{tool.Kind} · Ø {tool.NominalDiameter:0.###} mm · {tool.Name}",
            $"Halter: {holder?.Name ?? "kein Halter"}",
            $"Tech: {tool.TechCode ?? "—"} · Material: {tool.Material}"
        };

        if (tool.CornerRadius.HasValue && tool.CornerRadius.Value > 0)
        {
            lines.Add($"Eckenradius: R {tool.CornerRadius.Value:0.###} mm");
        }

        if (tool.Kind == ToolKind.Drill)
        {
            lines.Add("Werkzeugbild: Zylinder mit Schaft");
        }

        if (tool.Kind == ToolKind.Saw)
        {
            lines.Add("Werkzeugbild: Rueckwandnuter-Scheibe");
        }

        if (tool.IsFixedAggregate)
        {
            lines.Add("Montage: fix auf Bohr-/Saegeaggregat");
        }

        if (tool.MotionProfile == ToolMotionProfile.LinearXyOnly)
        {
            lines.Add("Kinematik: nur gerade Linien in X/Y");
        }

        if (tool.SpindleSpeed.HasValue || tool.FeedRate.HasValue)
        {
            lines.Add($"n = {FormatDouble(tool.SpindleSpeed)} rpm · vf = {FormatDouble(tool.FeedRate)} mm/min");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildHolderPreviewSummary(ToolHolderDefinition holder, ToolDefinition? representativeTool)
    {
        var usageCount = _library.Tools.Count(tool =>
            string.Equals(tool.HolderId, holder.Id, StringComparison.OrdinalIgnoreCase));

        var lines = new List<string>
        {
            $"{holder.Kind} · {holder.Name}",
            $"Gauge L = {FormatDouble(holder.GaugeLength)} mm · Gauge Ø = {FormatDouble(holder.GaugeDiameter)} mm",
            $"Zugewiesene Werkzeuge: {usageCount}"
        };

        if (representativeTool != null)
        {
            lines.Add($"Preview mit: {representativeTool.Name} ({representativeTool.TechCode ?? "ohne Tech"})");
            if (representativeTool.MotionProfile == ToolMotionProfile.LinearXyOnly)
            {
                lines.Add("Aggregat-Richtung: nur X/Y-gerade");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void CreateNewTool()
    {
        var kind = ToolKind.Router;
        var tool = new ToolDefinition
        {
            Id = CreateUniqueId("tool", _library.Tools.Select(existing => existing.Id)),
            Name = CreateUniqueDisplayName("Neues Werkzeug", _library.Tools.Select(existing => existing.Name)),
            Kind = kind,
            HolderId = GetSuggestedHolderId(kind),
            Material = ToolMaterial.Carbide,
            NominalDiameter = 6.0,
            ShankDiameter = 6.0,
            FluteCount = 2,
            SpindleSpeed = 18000,
            FeedRate = 3500,
            PlungeFeedRate = 1200,
            DefaultStepDown = 3.0,
            DefaultStepOver = 2.5,
            Description = "Neuer Werkzeug-Datensatz"
        };

        _library = _library.AddOrUpdate(tool);
        RefreshLibraryAfterMutation(tool.Id, tool.HolderId);
    }

    private void ApplyToolChanges()
    {
        try
        {
            var existingId = SelectedTool?.Id;
            var tool = BuildToolFromEditor(existingId);
            _library = _library.AddOrUpdate(tool);
            RefreshLibraryAfterMutation(tool.Id, tool.HolderId);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, MessageBoxType.Error);
        }
    }

    private void DeleteSelectedTool()
    {
        if (SelectedTool == null)
            return;

        _library = _library.Remove(SelectedTool.Id);
        RefreshLibraryAfterMutation(null, null);
    }

    private void DuplicateSelectedTool()
    {
        var source = SelectedTool ?? CreateDraftTool();
        var clone = source with
        {
            Id = CreateUniqueId(source.Id + "_copy", _library.Tools.Select(tool => tool.Id)),
            Name = CreateUniqueDisplayName($"{source.Name} Kopie", _library.Tools.Select(tool => tool.Name))
        };

        _library = _library.AddOrUpdate(clone);
        RefreshLibraryAfterMutation(clone.Id, clone.HolderId);
    }

    private ToolDefinition BuildToolFromEditor(string? existingId)
    {
        var name = RequireText(_toolNameTextBox.Text, "Werkzeugname");
        var idInput = string.IsNullOrWhiteSpace(_toolIdTextBox.Text)
            ? Slugify(name)
            : _toolIdTextBox.Text!;
        var id = CreateUniqueId(idInput, _library.Tools.Select(tool => tool.Id), existingId);
        var diameter = RequirePositiveDouble(_toolDiameterTextBox.Text, "Durchmesser");
        var fluteCount = ParseNullableInt(_toolFluteCountTextBox.Text, "Schneiden");

        return new ToolDefinition
        {
            Id = id,
            Name = name,
            Kind = GetEnumDropDownValue<ToolKind>(_toolKindDropDown),
            HolderId = GetSelectedHolderId(),
            TechCode = NormalizeOptionalText(_toolTechCodeTextBox.Text),
            NominalDiameter = diameter,
            ShankDiameter = ParseNullableDouble(_toolShankDiameterTextBox.Text, "Schaft-Durchmesser"),
            CornerRadius = ParseNullableDouble(_toolCornerRadiusTextBox.Text, "Eckenradius"),
            CuttingLength = ParseNullableDouble(_toolCuttingLengthTextBox.Text, "Schneidenlänge"),
            OverallLength = ParseNullableDouble(_toolOverallLengthTextBox.Text, "Gesamtlänge"),
            FluteCount = fluteCount,
            Material = GetEnumDropDownValue<ToolMaterial>(_toolMaterialDropDown),
            SpindleSpeed = ParseNullableDouble(_toolSpindleTextBox.Text, "Drehzahl"),
            FeedRate = ParseNullableDouble(_toolFeedTextBox.Text, "Vorschub"),
            PlungeFeedRate = ParseNullableDouble(_toolPlungeFeedTextBox.Text, "Eintauchvorschub"),
            DefaultStepDown = ParseNullableDouble(_toolStepDownTextBox.Text, "Stepdown"),
            DefaultStepOver = ParseNullableDouble(_toolStepOverTextBox.Text, "Stepover"),
            MotionProfile = GetDefaultMotionProfile(GetEnumDropDownValue<ToolKind>(_toolKindDropDown)),
            IsFixedAggregate = IsFixedAggregateByDefault(GetEnumDropDownValue<ToolKind>(_toolKindDropDown)),
            Description = NormalizeOptionalText(_toolDescriptionTextArea.Text)
        };
    }

    private ToolHolderDefinition BuildHolderFromEditor(string? existingId)
    {
        var name = RequireText(_holderNameTextBox.Text, "Haltername");
        var idInput = string.IsNullOrWhiteSpace(_holderIdTextBox.Text)
            ? Slugify(name)
            : _holderIdTextBox.Text!;
        var id = CreateUniqueId(idInput, _library.Holders.Select(holder => holder.Id), existingId);

        return new ToolHolderDefinition
        {
            Id = id,
            Name = name,
            Kind = GetEnumDropDownValue<HolderKind>(_holderKindDropDown),
            GaugeLength = ParseNullableDouble(_holderGaugeLengthTextBox.Text, "Gauge Length"),
            GaugeDiameter = ParseNullableDouble(_holderGaugeDiameterTextBox.Text, "Gauge Durchmesser"),
            ProjectionLength = ParseNullableDouble(_holderProjectionTextBox.Text, "Auskragung"),
            Description = NormalizeOptionalText(_holderDescriptionTextArea.Text)
        };
    }

    private void CreateNewHolder()
    {
        var holder = new ToolHolderDefinition
        {
            Id = CreateUniqueId("holder", _library.Holders.Select(existing => existing.Id)),
            Name = CreateUniqueDisplayName("Neuer Halter", _library.Holders.Select(existing => existing.Name)),
            Kind = HolderKind.Generic,
            GaugeLength = 110,
            GaugeDiameter = 40,
            ProjectionLength = 60,
            Description = "Neuer Halter-Datensatz"
        };

        _library = _library.AddOrUpdateHolder(holder);
        RefreshLibraryAfterMutation(null, holder.Id);
    }

    private void DuplicateSelectedHolder()
    {
        var source = SelectedHolder ?? CreateDraftHolder();
        var clone = source with
        {
            Id = CreateUniqueId(source.Id + "_copy", _library.Holders.Select(holder => holder.Id)),
            Name = CreateUniqueDisplayName($"{source.Name} Kopie", _library.Holders.Select(holder => holder.Name))
        };

        _library = _library.AddOrUpdateHolder(clone);
        RefreshLibraryAfterMutation(null, clone.Id);
    }

    private void ApplyHolderChanges()
    {
        try
        {
            var existingId = SelectedHolder?.Id;
            var holder = BuildHolderFromEditor(existingId);
            _library = _library.AddOrUpdateHolder(holder);
            RefreshLibraryAfterMutation(null, holder.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, MessageBoxType.Error);
        }
    }

    private void DeleteSelectedHolder()
    {
        if (SelectedHolder == null)
            return;

        _library = _library.RemoveHolder(SelectedHolder.Id);
        RefreshLibraryAfterMutation(null, null);
    }

    private string? GetSelectedHolderId()
    {
        var index = _toolHolderDropDown.SelectedIndex;
        if (index <= 0 || index - 1 >= _holderChoices.Count)
            return null;

        return _holderChoices[index - 1].Id;
    }

    private string? GetSuggestedHolderId(ToolKind kind)
    {
        var preferredKind = kind switch
        {
            ToolKind.Router => HolderKind.ColletChuck,
            ToolKind.Drill => HolderKind.DrillBlock,
            ToolKind.Saw => HolderKind.SawAggregate,
            ToolKind.Macro => HolderKind.MacroAggregate,
            _ => HolderKind.Generic
        };

        return _library.Holders.FirstOrDefault(holder => holder.Kind == preferredKind)?.Id
            ?? _library.Holders.FirstOrDefault()?.Id;
    }

    private static ToolMotionProfile GetDefaultMotionProfile(ToolKind kind)
    {
        return kind switch
        {
            ToolKind.Drill => ToolMotionProfile.PointOnly,
            ToolKind.Saw => ToolMotionProfile.LinearXyOnly,
            ToolKind.Macro => ToolMotionProfile.MacroDriven,
            _ => ToolMotionProfile.Freeform2D
        };
    }

    private static bool IsFixedAggregateByDefault(ToolKind kind)
    {
        return kind is ToolKind.Saw or ToolKind.Drill;
    }

    private ToolDefinition? SelectedTool
    {
        get
        {
            var index = _toolGrid.SelectedRow;
            return index >= 0 && index < _toolRows.Count ? _toolRows[index] : null;
        }
    }

    private ToolHolderDefinition? SelectedHolder
    {
        get
        {
            var index = _holderGrid.SelectedRow;
            return index >= 0 && index < _holderRows.Count ? _holderRows[index] : null;
        }
    }

    private ToolDefinition CreateDraftTool()
    {
        var fallback = SelectedTool ?? new ToolDefinition
        {
            Id = string.Empty,
            Name = "Neues Werkzeug",
            Kind = ToolKind.Router,
            Material = ToolMaterial.Carbide,
            NominalDiameter = 6.0
        };

        return new ToolDefinition
        {
            Id = NormalizeOptionalText(_toolIdTextBox.Text) ?? fallback.Id,
            Name = NormalizeOptionalText(_toolNameTextBox.Text) ?? fallback.Name,
            Kind = GetEnumDropDownValue<ToolKind>(_toolKindDropDown),
            HolderId = GetSelectedHolderId(),
            TechCode = NormalizeOptionalText(_toolTechCodeTextBox.Text) ?? fallback.TechCode,
            NominalDiameter = ParseDoubleOrFallback(_toolDiameterTextBox.Text, fallback.NominalDiameter),
            ShankDiameter = ParseDoubleOrNullable(_toolShankDiameterTextBox.Text, fallback.ShankDiameter),
            CornerRadius = ParseDoubleOrNullable(_toolCornerRadiusTextBox.Text, fallback.CornerRadius),
            CuttingLength = ParseDoubleOrNullable(_toolCuttingLengthTextBox.Text, fallback.CuttingLength),
            OverallLength = ParseDoubleOrNullable(_toolOverallLengthTextBox.Text, fallback.OverallLength),
            FluteCount = ParseIntOrNullable(_toolFluteCountTextBox.Text, fallback.FluteCount),
            Material = GetEnumDropDownValue<ToolMaterial>(_toolMaterialDropDown),
            SpindleSpeed = ParseDoubleOrNullable(_toolSpindleTextBox.Text, fallback.SpindleSpeed),
            FeedRate = ParseDoubleOrNullable(_toolFeedTextBox.Text, fallback.FeedRate),
            PlungeFeedRate = ParseDoubleOrNullable(_toolPlungeFeedTextBox.Text, fallback.PlungeFeedRate),
            DefaultStepDown = ParseDoubleOrNullable(_toolStepDownTextBox.Text, fallback.DefaultStepDown),
            DefaultStepOver = ParseDoubleOrNullable(_toolStepOverTextBox.Text, fallback.DefaultStepOver),
            MotionProfile = GetDefaultMotionProfile(GetEnumDropDownValue<ToolKind>(_toolKindDropDown)),
            IsFixedAggregate = IsFixedAggregateByDefault(GetEnumDropDownValue<ToolKind>(_toolKindDropDown)),
            Description = NormalizeOptionalText(_toolDescriptionTextArea.Text) ?? fallback.Description
        };
    }

    private ToolHolderDefinition CreateDraftHolder()
    {
        var fallback = SelectedHolder ?? new ToolHolderDefinition
        {
            Id = string.Empty,
            Name = "Neuer Halter",
            Kind = HolderKind.Generic
        };

        return new ToolHolderDefinition
        {
            Id = NormalizeOptionalText(_holderIdTextBox.Text) ?? fallback.Id,
            Name = NormalizeOptionalText(_holderNameTextBox.Text) ?? fallback.Name,
            Kind = GetEnumDropDownValue<HolderKind>(_holderKindDropDown),
            GaugeLength = ParseDoubleOrNullable(_holderGaugeLengthTextBox.Text, fallback.GaugeLength),
            GaugeDiameter = ParseDoubleOrNullable(_holderGaugeDiameterTextBox.Text, fallback.GaugeDiameter),
            ProjectionLength = ParseDoubleOrNullable(_holderProjectionTextBox.Text, fallback.ProjectionLength),
            Description = NormalizeOptionalText(_holderDescriptionTextArea.Text) ?? fallback.Description
        };
    }

    private static TableRow CreateEditorRow(string label, Control control)
    {
        return new TableRow(
            new TableCell(new Label { Text = label, VerticalAlignment = VerticalAlignment.Center }),
            new TableCell(control, true));
    }

    private static GroupBox CreateEditorSection(string title, Control content)
    {
        return new GroupBox
        {
            Text = title,
            Padding = new Padding(10),
            Content = content
        };
    }

    private static TextBox CreateTextBox() => new();

    private static TextArea CreateTextArea()
    {
        return new TextArea
        {
            Height = 72,
            Wrap = true
        };
    }

    private static Control CreateHintLabel(string text)
    {
        return new Label
        {
            Text = text,
            TextColor = Color.FromArgb(95, 95, 95)
        };
    }

    private static Label CreatePreviewSummaryLabel()
    {
        return new Label
        {
            Text = string.Empty,
            TextColor = Color.FromArgb(70, 70, 70)
        };
    }

    private static DropDown CreateEnumDropDown<TEnum>() where TEnum : struct, Enum
    {
        var dropDown = new DropDown();
        foreach (var value in Enum.GetValues<TEnum>())
        {
            dropDown.Items.Add(value.ToString());
        }

        dropDown.SelectedIndex = 0;
        return dropDown;
    }

    private static void SetEnumDropDownValue<TEnum>(DropDown dropDown, TEnum value)
        where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        var index = Array.IndexOf(values, value);
        dropDown.SelectedIndex = index >= 0 ? index : 0;
    }

    private static TEnum GetEnumDropDownValue<TEnum>(DropDown dropDown)
        where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        var index = dropDown.SelectedIndex;
        if (index < 0 || index >= values.Length)
            return values[0];

        return values[index];
    }

    private static string FormatDouble(double? value)
    {
        return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static double ParseDoubleOrFallback(string? text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static double? ParseDoubleOrNullable(string? text, double? fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static int? ParseIntOrNullable(string? text, int? fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static double? ParseNullableDouble(string? text, string fieldName)
    {
        var normalized = NormalizeOptionalText(text);
        if (normalized == null)
            return null;

        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        throw new InvalidOperationException($"{fieldName} muss eine Zahl im Invariant-Format sein.");
    }

    private static int? ParseNullableInt(string? text, string fieldName)
    {
        var normalized = NormalizeOptionalText(text);
        if (normalized == null)
            return null;

        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return value;

        throw new InvalidOperationException($"{fieldName} muss eine ganze Zahl sein.");
    }

    private static double RequirePositiveDouble(string? text, string fieldName)
    {
        var value = ParseNullableDouble(text, fieldName);
        if (!value.HasValue || value.Value <= 0)
            throw new InvalidOperationException($"{fieldName} muss größer als 0 sein.");

        return value.Value;
    }

    private static string RequireText(string? text, string fieldName)
    {
        var normalized = NormalizeOptionalText(text);
        if (normalized == null)
            throw new InvalidOperationException($"{fieldName} darf nicht leer sein.");

        return normalized;
    }

    private static string? NormalizeOptionalText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string CreateUniqueId(string candidate, IEnumerable<string> existingIds, string? currentId = null)
    {
        var baseId = Slugify(candidate);
        if (string.IsNullOrWhiteSpace(baseId))
            baseId = "item";

        var reserved = new HashSet<string>(
            existingIds
                .Where(id => !string.Equals(id, currentId, StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

        if (!reserved.Contains(baseId))
            return baseId;

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var next = $"{baseId}_{suffix}";
            if (!reserved.Contains(next))
                return next;
        }

        throw new InvalidOperationException("Es konnte keine eindeutige ID erzeugt werden.");
    }

    private static string CreateUniqueDisplayName(string baseName, IEnumerable<string> existingNames)
    {
        var reserved = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        if (!reserved.Contains(baseName))
            return baseName;

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var next = $"{baseName} {suffix}";
            if (!reserved.Contains(next))
                return next;
        }

        return baseName;
    }

    private static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var chars = text
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();

        var normalized = new string(chars);
        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        return normalized.Trim('_');
    }

    private sealed class ToolAssemblyPreview : Drawable
    {
        private readonly string _emptyText;
        private ToolDefinition? _tool;
        private ToolHolderDefinition? _holder;

        public ToolAssemblyPreview(string emptyText)
        {
            _emptyText = emptyText;
            Size = new Size(320, 420);
            BackgroundColor = Color.FromArgb(250, 250, 252);
        }

        public void UpdatePreview(ToolDefinition? tool, ToolHolderDefinition? holder)
        {
            _tool = tool;
            _holder = holder;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            var width = Math.Max(ClientSize.Width, 280);
            var height = Math.Max(ClientSize.Height, 320);

            using var bgBrush = new SolidBrush(Color.FromArgb(250, 250, 252));
            using var panelBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
            using var holderBrush = new SolidBrush(Color.FromArgb(150, 158, 170));
            using var holderCapBrush = new SolidBrush(Color.FromArgb(110, 118, 128));
            using var toolBrush = new SolidBrush(GetToolColor(_tool?.Kind ?? ToolKind.Router));
            using var borderPen = new Pen(Color.FromArgb(205, 210, 218), 1);
            using var centerPen = new Pen(Color.FromArgb(220, 224, 230), 1);

            g.FillRectangle(bgBrush, 0, 0, width, height);
            g.FillRectangle(panelBrush, 6, 6, width - 12, height - 12);
            g.DrawRectangle(borderPen, 6, 6, width - 12, height - 12);
            g.DrawText(new Font(SystemFont.Bold, 10), Colors.Black, 18, 14, "Preview");

            var previewRect = new RectangleF(22, 38, width - 44, height - 70);
            g.DrawRectangle(borderPen, previewRect);

            if (_tool == null && _holder == null)
            {
                g.DrawText(new Font(SystemFont.Default, 9), Colors.DimGray, previewRect.X + 16, previewRect.Y + 20, _emptyText);
                return;
            }

            var holderDiameter = (float)Math.Max(_holder?.GaugeDiameter ?? 0, (_tool?.NominalDiameter ?? 10) * 2.2);
            var holderLength = (float)Math.Max(_holder?.GaugeLength ?? 0, 70);
            var toolDiameter = (float)Math.Max(_tool?.NominalDiameter ?? 8, 5);
            var toolLength = (float)Math.Max(
                _tool?.OverallLength
                ?? _tool?.CuttingLength
                ?? _holder?.ProjectionLength
                ?? 60,
                55);

            var totalLength = holderLength + toolLength + 30;
            var scaleY = (previewRect.Height - 48) / totalLength;
            var scaleX = (previewRect.Width - 70) / Math.Max(holderDiameter, toolDiameter * 4f);
            var scale = Math.Min(scaleY, scaleX);

            var centerX = previewRect.X + previewRect.Width / 2f;
            var topY = previewRect.Y + 20;
            g.DrawLine(centerPen, centerX, previewRect.Y + 8, centerX, previewRect.Bottom - 8);

            var holderWidth = Math.Clamp(holderDiameter * scale, 42f, previewRect.Width - 30);
            var holderHeight = Math.Clamp(holderLength * scale, 86f, previewRect.Height * 0.55f);
            var holderRect = new RectangleF(centerX - holderWidth / 2f, topY, holderWidth, holderHeight);

            if (_holder != null)
            {
                g.FillRectangle(holderBrush, holderRect);
                g.FillRectangle(holderCapBrush, holderRect.X, holderRect.Y, holderRect.Width, Math.Min(18f, holderRect.Height / 4f));
                g.DrawRectangle(borderPen, holderRect);
            }

            if (_tool != null)
            {
                var shaftY = _holder != null ? holderRect.Bottom : topY;
                var shaftWidth = Math.Clamp(toolDiameter * scale, 10f, holderRect.Width > 0 ? holderRect.Width - 10f : 26f);
                var shaftHeight = Math.Clamp(toolLength * scale, 70f, previewRect.Bottom - shaftY - 20f);
                var shaftRect = new RectangleF(centerX - shaftWidth / 2f, shaftY, shaftWidth, shaftHeight);

                if (_tool.Kind == ToolKind.Saw)
                {
                    var arborHeight = Math.Max(22f, shaftHeight * 0.22f);
                    var arborWidth = Math.Max(18f, shaftWidth * 1.15f);
                    var arborRect = new RectangleF(centerX - arborWidth / 2f, shaftRect.Y, arborWidth, arborHeight);
                    g.FillRectangle(toolBrush, arborRect);
                    g.DrawRectangle(borderPen, arborRect);

                    var discDiameter = Math.Clamp(Math.Max(holderRect.Width * 0.82f, previewRect.Width * 0.42f), 60f, previewRect.Width - 34f);
                    var discRect = new RectangleF(centerX - discDiameter / 2f, arborRect.Bottom - discDiameter * 0.18f, discDiameter, discDiameter);
                    g.FillEllipse(toolBrush, discRect);
                    g.DrawEllipse(borderPen, discRect);

                    var hubDiameter = discDiameter * 0.26f;
                    var hubRect = new RectangleF(centerX - hubDiameter / 2f, discRect.Y + discDiameter / 2f - hubDiameter / 2f, hubDiameter, hubDiameter);
                    g.FillEllipse(panelBrush, hubRect);
                    g.DrawEllipse(borderPen, hubRect);

                    var kerfText = $"Nutbreite {FormatValue(_tool.NominalDiameter)}";
                    g.DrawText(new Font(SystemFont.Default, 8), Colors.DimGray, previewRect.X + 8, previewRect.Y + 10, kerfText);
                }
                else if (_tool.Kind == ToolKind.Drill)
                {
                    using var drillBodyBrush = new SolidBrush(Color.FromArgb(82, 88, 96));
                    using var drillTipBrush = new SolidBrush(Color.FromArgb(46, 50, 56));

                    var shankHeight = shaftHeight * 0.42f;
                    var shankRect = new RectangleF(centerX - shaftWidth / 2f, shaftRect.Y, shaftWidth, shankHeight);
                    g.FillRectangle(toolBrush, shankRect);
                    g.DrawRectangle(borderPen, shankRect);

                    var shankTopEllipse = new RectangleF(shankRect.X, shankRect.Y - Math.Min(6f, shankRect.Width / 3f), shankRect.Width, Math.Min(12f, shankRect.Width * 0.6f));
                    g.FillEllipse(toolBrush, shankTopEllipse);
                    g.DrawEllipse(borderPen, shankTopEllipse);

                    var bodyWidth = Math.Max(shaftWidth * 0.62f, 8f);
                    var bodyHeight = shaftHeight * 0.38f;
                    var bodyRect = new RectangleF(centerX - bodyWidth / 2f, shankRect.Bottom - 1f, bodyWidth, bodyHeight);
                    g.FillRectangle(drillBodyBrush, bodyRect);
                    g.DrawRectangle(borderPen, bodyRect);

                    var bodyTopEllipse = new RectangleF(bodyRect.X, bodyRect.Y - Math.Min(4f, bodyRect.Width / 3f), bodyRect.Width, Math.Min(8f, bodyRect.Width * 0.55f));
                    g.FillEllipse(drillBodyBrush, bodyTopEllipse);
                    g.DrawEllipse(borderPen, bodyTopEllipse);

                    var tipHeight = Math.Max(18f, shaftHeight * 0.20f);
                    var tipPoints = new[]
                    {
                        new PointF(centerX - bodyWidth / 2f, bodyRect.Bottom),
                        new PointF(centerX + bodyWidth / 2f, bodyRect.Bottom),
                        new PointF(centerX, bodyRect.Bottom + tipHeight)
                    };
                    g.FillPolygon(drillTipBrush, tipPoints);
                    g.DrawLine(borderPen, tipPoints[0], tipPoints[2]);
                    g.DrawLine(borderPen, tipPoints[1], tipPoints[2]);
                    g.DrawLine(borderPen, tipPoints[0], tipPoints[1]);
                }
                else
                {
                    var cuttingLength = (float)Math.Max(_tool.CuttingLength ?? toolLength * 0.45f, 20f);
                    var cuttingHeight = Math.Clamp(cuttingLength * scale, 18f, shaftRect.Height);
                    var fluteRect = new RectangleF(shaftRect.X, shaftRect.Bottom - cuttingHeight, shaftRect.Width, cuttingHeight);
                    var shankHeight = Math.Max(0f, shaftRect.Height - cuttingHeight);
                    using var fluteBrush = new SolidBrush(Color.FromArgb(34, 34, 34));

                    if (shankHeight > 0)
                    {
                        var shankRect = new RectangleF(shaftRect.X, shaftRect.Y, shaftRect.Width, shankHeight);
                        g.FillRectangle(toolBrush, shankRect);
                        g.DrawRectangle(borderPen, shankRect);
                    }

                    var cornerRadius = (float)Math.Max(_tool.CornerRadius ?? 0, 0);
                    if (cornerRadius > 0.01f)
                    {
                        var radiusPx = Math.Clamp(cornerRadius * scale, 2.5f, Math.Min(fluteRect.Width / 2f - 1f, fluteRect.Height / 2f - 1f));
                        if (radiusPx > 1f)
                        {
                            var upperFluteHeight = Math.Max(0f, fluteRect.Height - radiusPx);
                            if (upperFluteHeight > 0)
                            {
                                var fluteBodyRect = new RectangleF(fluteRect.X, fluteRect.Y, fluteRect.Width, upperFluteHeight);
                                g.FillRectangle(fluteBrush, fluteBodyRect);
                                g.DrawRectangle(borderPen, fluteBodyRect);
                            }

                            var bottomBridgeWidth = Math.Max(0f, fluteRect.Width - 2f * radiusPx);
                            if (bottomBridgeWidth > 0)
                            {
                                var bottomBridgeRect = new RectangleF(
                                    fluteRect.X + radiusPx,
                                    fluteRect.Bottom - radiusPx,
                                    bottomBridgeWidth,
                                    radiusPx);
                                g.FillRectangle(fluteBrush, bottomBridgeRect);
                                g.DrawRectangle(borderPen, bottomBridgeRect);
                            }

                            var leftRadiusRect = new RectangleF(
                                fluteRect.X,
                                fluteRect.Bottom - 2f * radiusPx,
                                2f * radiusPx,
                                2f * radiusPx);
                            var rightRadiusRect = new RectangleF(
                                fluteRect.Right - 2f * radiusPx,
                                fluteRect.Bottom - 2f * radiusPx,
                                2f * radiusPx,
                                2f * radiusPx);

                            g.FillEllipse(fluteBrush, leftRadiusRect);
                            g.FillEllipse(fluteBrush, rightRadiusRect);
                            g.DrawEllipse(borderPen, leftRadiusRect);
                            g.DrawEllipse(borderPen, rightRadiusRect);
                        }
                        else
                        {
                            g.FillRectangle(fluteBrush, fluteRect);
                            g.DrawRectangle(borderPen, fluteRect);
                        }
                    }
                    else
                    {
                        g.FillRectangle(fluteBrush, fluteRect);
                        g.DrawRectangle(borderPen, fluteRect);
                    }
                }
            }

            var metaY = previewRect.Bottom - 42;
            g.DrawText(new Font(SystemFont.Default, 8), Colors.DimGray, previewRect.X + 8, metaY, $"Holder Ø {FormatValue(_holder?.GaugeDiameter)} / L {FormatValue(_holder?.GaugeLength)}");
            var toolMeta = $"Tool Ø {FormatValue(_tool?.NominalDiameter)} / L {FormatValue(_tool?.OverallLength ?? _tool?.CuttingLength)}";
            if (_tool?.CornerRadius is > 0)
            {
                toolMeta += $" / R {FormatValue(_tool.CornerRadius)}";
            }

            g.DrawText(new Font(SystemFont.Default, 8), Colors.DimGray, previewRect.X + 8, metaY + 16, toolMeta);
        }

        private static Color GetToolColor(ToolKind kind)
        {
            return kind switch
            {
                ToolKind.Drill => Color.FromArgb(228, 179, 52),
                ToolKind.Saw => Color.FromArgb(218, 120, 52),
                ToolKind.Macro => Color.FromArgb(58, 160, 152),
                _ => Color.FromArgb(72, 126, 196)
            };
        }

        private static string FormatValue(double? value)
        {
            return value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—";
        }
    }
}
