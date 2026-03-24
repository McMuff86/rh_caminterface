using System.Globalization;
using Eto.Drawing;
using Eto.Forms;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.UI;

public sealed class ToolStrategyDialog : Dialog<IReadOnlyList<MachiningToolOverride>?>
{
    private readonly ToolLibrary _library;
    private readonly IReadOnlyList<OperationStrategyItem> _items;
    private readonly ToolpathPlanningOptions _planningOptions;
    private readonly Dictionary<string, MachiningToolOverride> _overrides;
    private readonly List<ToolHolderDefinition> _holderChoices;

    private readonly Label _summaryLabel;
    private readonly GridView _operationGrid;
    private readonly Label _operationLabel;
    private readonly Label _detailsLabel;
    private readonly Label _autoStrategyLabel;
    private readonly Label _resolvedStrategyLabel;
    private readonly DropDown _finishToolDropDown;
    private readonly DropDown _finishHolderDropDown;
    private readonly DropDown _roughToolDropDown;
    private readonly DropDown _roughHolderDropDown;

    private List<OperationStrategyItem> _rows = new();
    private List<ToolDefinition> _currentCompatibleTools = new();
    private bool _isLoading;

    public ToolStrategyDialog(
        ToolLibrary library,
        IReadOnlyList<OperationStrategyItem> items,
        IReadOnlyList<MachiningToolOverride>? existingOverrides,
        ToolpathPlanningOptions planningOptions)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _planningOptions = planningOptions ?? throw new ArgumentNullException(nameof(planningOptions));
        _overrides = (existingOverrides ?? Array.Empty<MachiningToolOverride>())
            .Where(static item => item.HasOverride)
            .ToDictionary(item => item.OperationKey, item => item, StringComparer.OrdinalIgnoreCase);
        _holderChoices = library.Holders
            .OrderBy(static holder => holder.Kind)
            .ThenBy(static holder => holder.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Title = $"Werkzeugzuordnung - {library.Name}";
        ClientSize = new Size(1220, 760);
        MinimumSize = new Size(1040, 660);
        Padding = new Padding(12);
        Resizable = true;

        _summaryLabel = new Label
        {
            Text = string.Empty,
            Font = new Font(SystemFont.Bold, 11)
        };

        _operationGrid = CreateOperationGrid();
        _operationGrid.SelectionChanged += (_, _) => LoadSelectedOperation();

        _operationLabel = CreateInfoLabel();
        _detailsLabel = CreateInfoLabel();
        _autoStrategyLabel = CreateInfoLabel();
        _resolvedStrategyLabel = CreateInfoLabel();

        _finishToolDropDown = new DropDown();
        _finishToolDropDown.SelectedIndexChanged += (_, _) => OnOverrideChanged();
        _finishHolderDropDown = new DropDown();
        _finishHolderDropDown.SelectedIndexChanged += (_, _) => OnOverrideChanged();
        _roughToolDropDown = new DropDown();
        _roughToolDropDown.SelectedIndexChanged += (_, _) => OnOverrideChanged();
        _roughHolderDropDown = new DropDown();
        _roughHolderDropDown.SelectedIndexChanged += (_, _) => OnOverrideChanged();

        var resetSelectedButton = new Button { Text = "Auswahl zurücksetzen", Width = 170 };
        resetSelectedButton.Click += (_, _) => ResetSelectedOverride();

        var resetAllButton = new Button { Text = "Alle Overrides löschen", Width = 170 };
        resetAllButton.Click += (_, _) => ResetAllOverrides();

        var leftPane = new StackLayout
        {
            Spacing = 8,
            Padding = new Padding(0, 0, 8, 0),
            Items =
            {
                CreateHintLabel("Bearbeitungen der aktuell gewählten Platten. Änderungen auf der rechten Seite aktualisieren die Vorschau-Strategie sofort."),
                new StackLayoutItem(_operationGrid, true),
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Items = { resetSelectedButton, resetAllButton }
                }
            }
        };

        var editor = new Scrollable
        {
            Border = BorderType.None,
            Content = new StackLayout
            {
                Padding = new Padding(8, 0, 0, 0),
                Spacing = 10,
                Items =
                {
                    CreateGroup("Bearbeitung", new StackLayout
                    {
                        Spacing = 4,
                        Items = { _operationLabel, _detailsLabel }
                    }),
                    CreateGroup("Auto-Strategie", _autoStrategyLabel),
                    CreateGroup("Override", new TableLayout
                    {
                        Spacing = new Size(10, 6),
                        Rows =
                        {
                            CreateRow("Finish Werkzeug", _finishToolDropDown),
                            CreateRow("Finish Halter", _finishHolderDropDown),
                            CreateRow("Rough Werkzeug", _roughToolDropDown),
                            CreateRow("Rough Halter", _roughHolderDropDown)
                        }
                    }),
                    CreateGroup("Aktuelle Auflösung", _resolvedStrategyLabel)
                }
            }
        };

        var saveButton = new Button { Text = "Übernehmen", Width = 120 };
        saveButton.Click += (_, _) =>
        {
            Result = _overrides.Values
                .Where(static item => item.HasOverride)
                .OrderBy(static item => item.OperationKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Close();
        };

        var cancelButton = new Button { Text = "Abbrechen", Width = 120 };
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
                new Splitter
                {
                    Orientation = Orientation.Horizontal,
                    Position = 620,
                    Panel1 = leftPane,
                    Panel2 = editor
                },
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalContentAlignment = HorizontalAlignment.Right,
                    Spacing = 6,
                    Items = { saveButton, cancelButton }
                }
            }
        };

        RefreshSummary();
        ReloadGrid();
    }

    private static GridView CreateOperationGrid()
    {
        var grid = new GridView
        {
            Height = 520
        };

        grid.Columns.Add(CreateTextColumn("Platte", 0, 150));
        grid.Columns.Add(CreateTextColumn("Bearbeitung", 1, 220));
        grid.Columns.Add(CreateTextColumn("Typ", 2, 110));
        grid.Columns.Add(CreateTextColumn("Finish", 3, 220));
        grid.Columns.Add(CreateTextColumn("Rough", 4, 220));
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

    private void ReloadGrid(string? selectedOperationKey = null)
    {
        _rows = _items
            .OrderBy(static item => item.PlateName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.MachiningType)
            .ThenBy(static item => item.OperationName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _operationGrid.DataStore = _rows
            .Select(item => (object?)new object?[]
            {
                item.PlateName,
                item.OperationName,
                item.MachiningType.ToString(),
                FormatStrategyCell(ResolveStrategy(item).FinishingTool, ResolveStrategy(item).FinishingTool?.HolderId),
                item.SupportsRoughing
                    ? FormatStrategyCell(ResolveStrategy(item).RoughingTool, ResolveStrategy(item).RoughingTool?.HolderId)
                    : "—"
            })
            .ToList();

        if (_rows.Count == 0)
        {
            _operationLabel.Text = "Keine Bearbeitungen für die gewählten Platten.";
            _detailsLabel.Text = string.Empty;
            _autoStrategyLabel.Text = string.Empty;
            _resolvedStrategyLabel.Text = string.Empty;
            return;
        }

        var index = selectedOperationKey == null
            ? 0
            : _rows.FindIndex(item => string.Equals(item.OperationKey, selectedOperationKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            index = 0;

        _operationGrid.SelectedRow = index;
        LoadSelectedOperation();
    }

    private void LoadSelectedOperation()
    {
        var item = SelectedItem;
        if (item == null)
            return;

        _isLoading = true;

        var overrideItem = FindOverride(item.OperationKey);
        var autoStrategy = item.AutoStrategy;
        var resolvedStrategy = ResolveStrategy(item);

        _operationLabel.Text = $"{item.PlateName} · {item.OperationName}";
        _detailsLabel.Text = BuildMachiningDetails(item.Machining);
        _autoStrategyLabel.Text = BuildStrategySummary(autoStrategy, item.SupportsRoughing);
        _resolvedStrategyLabel.Text = BuildStrategySummary(resolvedStrategy, item.SupportsRoughing);

        _currentCompatibleTools = _library.GetCompatibleTools(item.Machining).ToList();
        PopulateToolDropDown(_finishToolDropDown, _currentCompatibleTools, overrideItem?.FinishingToolId);
        PopulateHolderDropDown(_finishHolderDropDown, overrideItem?.FinishingHolderId);

        var roughEnabled = item.SupportsRoughing;
        _roughToolDropDown.Enabled = roughEnabled;
        _roughHolderDropDown.Enabled = roughEnabled;
        PopulateToolDropDown(_roughToolDropDown, roughEnabled ? _currentCompatibleTools : Array.Empty<ToolDefinition>(), overrideItem?.RoughingToolId);
        PopulateHolderDropDown(_roughHolderDropDown, roughEnabled ? overrideItem?.RoughingHolderId : null);

        if (!roughEnabled)
        {
            _roughToolDropDown.SelectedIndex = 0;
            _roughHolderDropDown.SelectedIndex = 0;
        }

        _isLoading = false;
    }

    private void OnOverrideChanged()
    {
        if (_isLoading || SelectedItem == null)
            return;

        var item = SelectedItem;
        var roughEnabled = item!.SupportsRoughing;
        var overrideItem = new MachiningToolOverride
        {
            OperationKey = item.OperationKey,
            FinishingToolId = GetSelectedToolId(_finishToolDropDown, _currentCompatibleTools),
            FinishingHolderId = GetSelectedHolderId(_finishHolderDropDown),
            RoughingToolId = roughEnabled ? GetSelectedToolId(_roughToolDropDown, _currentCompatibleTools) : null,
            RoughingHolderId = roughEnabled ? GetSelectedHolderId(_roughHolderDropDown) : null
        };

        if (overrideItem.HasOverride)
            _overrides[item.OperationKey] = overrideItem;
        else
            _overrides.Remove(item.OperationKey);

        RefreshSummary();
        _resolvedStrategyLabel.Text = BuildStrategySummary(ResolveStrategy(item), item.SupportsRoughing);
        ReloadGrid(item.OperationKey);
    }

    private void ResetSelectedOverride()
    {
        var item = SelectedItem;
        if (item == null)
            return;

        _overrides.Remove(item.OperationKey);
        RefreshSummary();
        ReloadGrid(item.OperationKey);
    }

    private void ResetAllOverrides()
    {
        _overrides.Clear();
        RefreshSummary();
        ReloadGrid(SelectedItem?.OperationKey);
    }

    private void RefreshSummary()
    {
        _summaryLabel.Text =
            $"{_items.Count} Bearbeitungen · {_overrides.Count} Override(s) · Maschine: {_library.MachineKey}";
    }

    private OperationStrategyItem? SelectedItem
    {
        get
        {
            var index = _operationGrid.SelectedRow;
            return index >= 0 && index < _rows.Count ? _rows[index] : null;
        }
    }

    private MachiningToolOverride? FindOverride(string operationKey)
    {
        return _overrides.TryGetValue(operationKey, out var value) ? value : null;
    }

    private MachiningStrategy ResolveStrategy(OperationStrategyItem item)
    {
        return MachiningStrategy.CreateDefault(
            item.Machining,
            _library,
            _planningOptions,
            FindOverride(item.OperationKey));
    }

    private static void PopulateToolDropDown(
        DropDown dropDown,
        IEnumerable<ToolDefinition> tools,
        string? selectedToolId)
    {
        dropDown.Items.Clear();
        dropDown.Items.Add("(Automatisch)");

        var choiceList = tools.ToList();
        foreach (var tool in choiceList)
        {
            dropDown.Items.Add($"{tool.Name} [{tool.TechCode ?? "ohne Tech"}] · Ø {tool.NominalDiameter:0.###}");
        }

        if (string.IsNullOrWhiteSpace(selectedToolId))
        {
            dropDown.SelectedIndex = 0;
            return;
        }

        var index = choiceList.FindIndex(tool => string.Equals(tool.Id, selectedToolId, StringComparison.OrdinalIgnoreCase));
        dropDown.SelectedIndex = index >= 0 ? index + 1 : 0;
    }

    private void PopulateHolderDropDown(DropDown dropDown, string? selectedHolderId)
    {
        dropDown.Items.Clear();
        dropDown.Items.Add("(aus Werkzeug)");

        foreach (var holder in _holderChoices)
        {
            dropDown.Items.Add($"{holder.Name} [{holder.Kind}]");
        }

        if (string.IsNullOrWhiteSpace(selectedHolderId))
        {
            dropDown.SelectedIndex = 0;
            return;
        }

        var index = _holderChoices.FindIndex(holder => string.Equals(holder.Id, selectedHolderId, StringComparison.OrdinalIgnoreCase));
        dropDown.SelectedIndex = index >= 0 ? index + 1 : 0;
    }

    private static string? GetSelectedToolId(DropDown dropDown, IReadOnlyList<ToolDefinition> tools)
    {
        var index = dropDown.SelectedIndex;
        if (index <= 0 || index - 1 >= tools.Count)
            return null;

        return tools[index - 1].Id;
    }

    private string? GetSelectedHolderId(DropDown dropDown)
    {
        var index = dropDown.SelectedIndex;
        if (index <= 0 || index - 1 >= _holderChoices.Count)
            return null;

        return _holderChoices[index - 1].Id;
    }

    private static string BuildMachiningDetails(Machining machining)
    {
        return machining switch
        {
            DrillMachining drill => $"Bohrung · Ø {drill.Diameter:0.###} · Z {drill.Depth:0.###} · X {drill.X:0.###} / Y {drill.Y:0.###}",
            DrillPatternMachining pattern => $"Raster · Ø {pattern.Diameter:0.###} · {pattern.CountX}x{pattern.CountY} · Z {pattern.Depth:0.###}",
            HorizontalDrillMachining horizontal => $"Horizontalbohrung · Ø {horizontal.Diameter:0.###} · Z {horizontal.Depth:0.###} · Seite {horizontal.DrillSide}",
            PocketMachining pocket => $"Tasche · Ø {pocket.ToolDiameter:0.###} · Z {pocket.Depth:0.###} · Loops {pocket.Loops.Count}",
            RoutingMachining routing => $"Routing · Ø {routing.ToolDiameter:0.###} · Z {routing.Depth:0.###} · Punkte {routing.Points.Count}",
            RoutingWithArcsMachining routing => $"Routing+Arcs · Ø {routing.ToolDiameter:0.###} · Z {routing.Depth:0.###} · Segmente {routing.Segments.Count}",
            GrooveRntMachining groove => $"Rueckwandnut · C{groove.RntCode} · W {groove.Width:0.###} · Z {groove.Depth:0.###}",
            MacroMachining macro => $"Makro · {macro.MacroName} · Parameter {macro.Parameters.Count}",
            _ => machining.Name
        };
    }

    private string BuildStrategySummary(MachiningStrategy strategy, bool supportsRoughing)
    {
        var lines = new List<string>
        {
            $"Finish: {FormatStrategyCell(strategy.FinishingTool, strategy.FinishingTool?.HolderId)}"
        };

        if (supportsRoughing)
        {
            lines.Add($"Rough: {FormatStrategyCell(strategy.RoughingTool, strategy.RoughingTool?.HolderId)}");
            lines.Add(strategy.HasRoughingPass
                ? $"Aufmass: {_planningOptions.DefaultStockToLeave:0.###} mm"
                : "Aufmass: kein separater Rough-Pass");
        }
        else
        {
            lines.Add("Rough: nicht aktiv für diese Bearbeitung");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string FormatStrategyCell(ToolDefinition? tool, string? holderId)
    {
        if (tool == null)
            return "Auto/ohne Werkzeug";

        var holder = _library.FindHolderById(holderId);
        var holderText = holder?.Name ?? "kein Halter";
        return $"{tool.Name} [{tool.TechCode ?? "n/a"}] · {holderText}";
    }

    private static GroupBox CreateGroup(string title, Control content)
    {
        return new GroupBox
        {
            Text = title,
            Padding = new Padding(10),
            Content = content
        };
    }

    private static TableRow CreateRow(string label, Control control)
    {
        return new TableRow(
            new TableCell(new Label
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center
            }),
            new TableCell(control, true));
    }

    private static Label CreateInfoLabel()
    {
        return new Label
        {
            Text = string.Empty,
            TextColor = Color.FromArgb(65, 65, 65)
        };
    }

    private static Label CreateHintLabel(string text)
    {
        return new Label
        {
            Text = text,
            TextColor = Color.FromArgb(95, 95, 95)
        };
    }
}

public sealed record OperationStrategyItem
{
    public required string OperationKey { get; init; }
    public required string PlateName { get; init; }
    public required string OperationName { get; init; }
    public required MachiningType MachiningType { get; init; }
    public required Machining Machining { get; init; }
    public required MachiningStrategy AutoStrategy { get; init; }
    public bool SupportsRoughing { get; init; }
}
