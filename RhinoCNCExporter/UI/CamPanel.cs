using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.UI;

/// <summary>
/// Dockable CAM panel showing all CNC operations in the document.
/// Inspired by RhinoCAM / Fusion 360 CAM panels.
/// Provides operations tree, inline editing, toolpath generation,
/// machine profile selector, and context menu interactions.
/// </summary>
[Guid("c7e3a1d5-4f82-4e9b-b3c6-8d2f1a5e7b09")]
public sealed class CamPanel : Panel
{
    public static readonly Guid PanelId = typeof(CamPanel).GUID;
    public const string PanelDisplayName = "CNC Operations";

    // Document UserText key for machine profile persistence
    private const string DOC_MACHINE_PROFILE_KEY = "CNC_MachineProfile";

    // --- Colors (match Rhino dark theme / ExportPanel style) ---
    private static readonly Color BgDark = Color.FromArgb(45, 45, 48);
    private static readonly Color BgSection = Color.FromArgb(55, 55, 58);
    private static readonly Color FgText = Color.FromArgb(220, 220, 220);
    private static readonly Color FgDim = Color.FromArgb(150, 150, 150);
    private static readonly Color AccentContour = Color.FromArgb(220, 60, 60);
    private static readonly Color AccentPocket = Color.FromArgb(60, 120, 220);
    private static readonly Color AccentDrill = Color.FromArgb(220, 200, 40);
    private static readonly Color AccentGroove = Color.FromArgb(60, 180, 80);

    // --- Machine Profile Definitions ---
    private static readonly (string Key, string DisplayName, Func<IMachineProfile> Factory)[] MachineProfiles =
    {
        ("xilog", "SCM (Xilog)", () => new ScmProfile()),
        ("biesse", "Biesse (CIX)", () => new BiesseProfile()),
        ("maestrocadt", "MaestroCadT", () => new MaestroCadTProfile()),
    };

    // --- UI Controls ---
    private readonly DropDown _machineDropDown;
    private readonly TreeGridView _operationsTree;
    private TreeGridItemCollection _treeItems = new();

    // Empty state label
    private readonly Label _emptyStateLabel;

    // Properties panel controls
    private readonly StackLayout _propertiesPanel;
    private readonly Label _propTypeLabel;
    private readonly Label _propObjectLabel;
    private readonly DropDown _propToolDropDown;
    private readonly TextBox _propDepthTextBox;
    private readonly DropDown _propStrategyDropDown;
    private readonly TextBox _propWidthTextBox;
    private readonly TextBox _propDiameterTextBox;
    private readonly TextBox _propStepoverTextBox;
    private readonly CheckBox _propPeckCheckBox;
    private readonly TextBox _propPeckDepthTextBox;
    private readonly DropDown _propRampEntryDropDown;
    private readonly Button _applyButton;

    // Dynamic property rows (shown/hidden based on operation type)
    private readonly TableRow _strategyRow;
    private readonly TableRow _widthRow;
    private readonly TableRow _diameterRow;
    private readonly TableRow _stepoverRow;
    private readonly TableRow _peckRow;
    private readonly TableRow _peckDepthRow;
    private readonly TableRow _rampEntryRow;

    // Status bar
    private readonly Label _statusLabel;

    // Validation button & results
    private readonly Button _validateButton;
    private readonly Label _validationResultLabel;

    // Export bridge
    private readonly InteractiveExportBridge _exportBridge = new();

    // 3D preview toggle
    private readonly CheckBox _3dPreviewCheckBox;

    // Statistics labels
    private readonly Label _statsLabel;

    // Defaults section (editable Standardwerte)
    private readonly StackLayout _defaultsPanel;
    private readonly DropDown _defaultsTypeDropDown;
    private readonly TextBox _defaultsDepthTextBox;
    private readonly TextBox _defaultsFeedrateTextBox;
    private readonly DropDown _defaultsStrategyDropDown;
    private readonly TextBox _defaultsStepoverTextBox;
    private readonly TextBox _defaultsWidthTextBox;
    private readonly TextBox _defaultsDiameterTextBox;
    private readonly CheckBox _defaultsPeckCheckBox;
    private readonly TextBox _defaultsPeckDepthTextBox;
    // Rows for conditional visibility
    private readonly TableRow _defStrategyRow;
    private readonly TableRow _defStepoverRow;
    private readonly TableRow _defWidthRow;
    private readonly TableRow _defDiameterRow;
    private readonly TableRow _defPeckRow;
    private readonly TableRow _defPeckDepthRow;

    // State
    private readonly ToolLibraryStore _toolLibraryStore = new();
    private List<ToolDefinition> _allTools = new();
    private OperationEntry? _selectedOperation;
    private bool _eventsHooked;
    private uint _lastDocSerialNumber;

    public CamPanel()
    {
        BackgroundColor = BgDark;

        // --- Header ---
        var headerLabel = CreateLabel("🔧 CNC Operations", 13, true);

        // --- Machine Profile Selector ---
        _machineDropDown = new DropDown { ToolTip = "CNC-Maschinenprofil wählen — beeinflusst Werkzeuge, Exportformat und Defaults" };
        foreach (var mp in MachineProfiles)
            _machineDropDown.Items.Add(new ListItem { Text = mp.DisplayName, Key = mp.Key });
        _machineDropDown.SelectedIndex = 0;
        _machineDropDown.SelectedIndexChanged += OnMachineProfileChanged;

        var machineRow = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Padding = new Padding(0, 2, 0, 4),
            Items =
            {
                CreateLabel("Maschine:", 9, false),
                new StackLayoutItem(_machineDropDown, true)
            }
        };

        // --- Quick-Add Toolbar ---
        var addContourBtn = CreateToolbarButton("+ Contour", AccentContour, "Konturfräsung hinzufügen (CNCAddContour)");
        addContourBtn.Click += (_, _) => RunCommand("CNCAddContour");
        var addPocketBtn = CreateToolbarButton("+ Pocket", AccentPocket, "Taschenfräsung hinzufügen (CNCAddPocket)");
        addPocketBtn.Click += (_, _) => RunCommand("CNCAddPocket");
        var addDrillBtn = CreateToolbarButton("+ Drill", AccentDrill, "Bohrung hinzufügen (CNCAddDrill)");
        addDrillBtn.Click += (_, _) => RunCommand("CNCAddDrill");
        var addGrooveBtn = CreateToolbarButton("+ Groove", AccentGroove, "Nut hinzufügen (CNCAddGroove)");
        addGrooveBtn.Click += (_, _) => RunCommand("CNCAddGroove");

        var toolbar = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { addContourBtn, addPocketBtn, addDrillBtn, addGrooveBtn }
        };

        // --- Operations TreeView ---
        _operationsTree = CreateOperationsTree();

        // Empty state
        _emptyStateLabel = CreateLabel(
            "Keine Bearbeitungen vorhanden.\n\nVerwende die CNCAdd*-Befehle oder die Toolbar-Buttons oben,\num Bearbeitungsoperationen hinzuzufügen.",
            9, false);
        _emptyStateLabel.TextColor = FgDim;
        _emptyStateLabel.TextAlignment = TextAlignment.Center;
        _emptyStateLabel.Visible = true;

        var opsContent = new StackLayout
        {
            Spacing = 0,
            Items = { _operationsTree, _emptyStateLabel }
        };

        var opsSection = CreateSection("Operationen", opsContent, null, true);

        // --- Properties Panel ---
        _propTypeLabel = CreateLabel("—", 10, true);
        _propObjectLabel = CreateLabel("—", 9, false);
        _propObjectLabel.TextColor = FgDim;

        _propToolDropDown = new DropDown { ToolTip = "Werkzeug für diese Operation auswählen" };
        _propDepthTextBox = new TextBox { PlaceholderText = "mm", Width = 80, ToolTip = "Bearbeitungstiefe in mm" };

        _propStrategyDropDown = new DropDown { ToolTip = "Bearbeitungsstrategie" };
        _propStrategyDropDown.Items.Add("Rough");
        _propStrategyDropDown.Items.Add("Finish");
        _propStrategyDropDown.Items.Add("Both");
        _propStrategyDropDown.SelectedIndex = 0;

        _propWidthTextBox = new TextBox { PlaceholderText = "mm", Width = 80, ToolTip = "Nutbreite in mm" };
        _propDiameterTextBox = new TextBox { PlaceholderText = "mm", Width = 80, ToolTip = "Bohrdurchmesser in mm" };
        _propStepoverTextBox = new TextBox { PlaceholderText = "%", Width = 80, ToolTip = "Zustellung in % des Werkzeugdurchmessers" };

        _propPeckCheckBox = new CheckBox { Text = "Peck drilling", TextColor = FgText, ToolTip = "Spänebrechendes Bohren ein/aus" };
        _propPeckDepthTextBox = new TextBox { PlaceholderText = "mm", Width = 80, ToolTip = "Peck-Tiefe pro Zustellung in mm" };

        _propRampEntryDropDown = new DropDown { ToolTip = "Eintauchstrategie für Taschenbearbeitung" };
        _propRampEntryDropDown.Items.Add("Straight");
        _propRampEntryDropDown.Items.Add("Spiral");
        _propRampEntryDropDown.Items.Add("Profile");
        _propRampEntryDropDown.SelectedIndex = 0;

        _applyButton = new Button { Text = "✓ Anwenden", Height = 28, ToolTip = "Änderungen auf die ausgewählte Operation anwenden" };
        _applyButton.Click += (_, _) => ApplyPropertyChanges();

        // Build property rows
        var toolRow = new TableRow(CreateLabel("Tool:", 9, false), new TableCell(_propToolDropDown, true));
        var depthRow = new TableRow(CreateLabel("Depth:", 9, false), new TableCell(_propDepthTextBox, true));
        _strategyRow = new TableRow(CreateLabel("Strategy:", 9, false), new TableCell(_propStrategyDropDown, true));
        _widthRow = new TableRow(CreateLabel("Width:", 9, false), new TableCell(_propWidthTextBox, true));
        _diameterRow = new TableRow(CreateLabel("Diameter:", 9, false), new TableCell(_propDiameterTextBox, true));
        _stepoverRow = new TableRow(CreateLabel("Stepover:", 9, false), new TableCell(_propStepoverTextBox, true));
        _peckRow = new TableRow(new TableCell(_propPeckCheckBox) { ScaleWidth = true });
        _peckDepthRow = new TableRow(CreateLabel("Peck depth:", 9, false), new TableCell(_propPeckDepthTextBox, true));
        _rampEntryRow = new TableRow(CreateLabel("Ramp entry:", 9, false), new TableCell(_propRampEntryDropDown, true));

        var propsTable = new TableLayout
        {
            Spacing = new Size(8, 4),
            Rows = { toolRow, depthRow, _strategyRow, _widthRow, _diameterRow, _stepoverRow, _peckRow, _peckDepthRow, _rampEntryRow }
        };

        _propertiesPanel = new StackLayout
        {
            Spacing = 6,
            Items =
            {
                _propTypeLabel,
                _propObjectLabel,
                propsTable,
                _applyButton
            }
        };

        var propsSection = CreateSection("Eigenschaften", _propertiesPanel, "Ausgewählte Operation", true);

        // --- Action Buttons ---
        var generateAllBtn = new Button { Text = "▶ Alle generieren", Height = 30, ToolTip = "Toolpaths für alle Operationen (neu-)erzeugen" };
        generateAllBtn.Click += (_, _) => GenerateAllToolpaths();
        var clearAllBtn = new Button { Text = "✕ Alle löschen", Height = 30, ToolTip = "Alle Toolpath-Vorschaugeometrien entfernen" };
        clearAllBtn.Click += (_, _) => ClearAllToolpaths();
        var refreshBtn = new Button { Text = "↻ Aktualisieren", Height = 26, ToolTip = "Operationsliste neu laden (F5)" };
        refreshBtn.Click += (_, _) => RefreshOperationsTree();

        var actionButtons = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { generateAllBtn, clearAllBtn, refreshBtn }
        };

        // --- 3D Preview Toggle ---
        _3dPreviewCheckBox = new CheckBox
        {
            Text = "3D Toolpath-Vorschau (mit Tiefe)",
            TextColor = FgText,
            Checked = false,
            ToolTip = "Toolpaths mit tatsächlicher Schnitttiefe anzeigen (Z-Offset)"
        };
        _3dPreviewCheckBox.CheckedChanged += (_, _) => OnToggle3DPreview();

        // --- Validation Button ---
        _validateButton = new Button { Text = "✔ Validieren", Height = 30, ToolTip = "Operationen vor dem Export auf Fehler prüfen" };
        _validateButton.Click += (_, _) => RunValidation();

        _validationResultLabel = CreateLabel("", 9, false);
        _validationResultLabel.TextColor = FgDim;
        _validationResultLabel.Visible = false;
        _validationResultLabel.Wrap = WrapMode.Word;

        // --- Export CNC Button ---
        var exportCncBtn = new Button { Text = "📤 Export CNC", Height = 34, ToolTip = "Interaktive Operationen als CNC-Programm exportieren" };
        exportCncBtn.Click += (_, _) => ExportInteractiveCnc();

        var exportRow = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items =
            {
                new StackLayoutItem(_validateButton, false),
                new StackLayoutItem(exportCncBtn, true)
            }
        };

        // --- Defaults Section (editable) ---
        _defaultsTypeDropDown = new DropDown { ToolTip = "Operationstyp für Standardwerte wählen" };
        _defaultsTypeDropDown.Items.Add(new ListItem { Text = "🔴 Kontur (Contour)", Key = "Contour" });
        _defaultsTypeDropDown.Items.Add(new ListItem { Text = "🔵 Tasche (Pocket)", Key = "Pocket" });
        _defaultsTypeDropDown.Items.Add(new ListItem { Text = "🟡 Bohrung (Drill)", Key = "Drill" });
        _defaultsTypeDropDown.Items.Add(new ListItem { Text = "🟢 Nut (Groove)", Key = "Groove" });
        _defaultsTypeDropDown.SelectedIndex = 0;
        _defaultsTypeDropDown.SelectedIndexChanged += (_, _) => OnDefaultsTypeChanged();

        _defaultsDepthTextBox = new TextBox { PlaceholderText = "mm", Width = 100, ToolTip = "Standard-Bearbeitungstiefe in mm" };
        _defaultsFeedrateTextBox = new TextBox { PlaceholderText = "mm/min", Width = 100, ToolTip = "Standard-Vorschubgeschwindigkeit in mm/min" };
        _defaultsStrategyDropDown = new DropDown { ToolTip = "Standard-Bearbeitungsstrategie" };
        _defaultsStrategyDropDown.Items.Add("Rough");
        _defaultsStrategyDropDown.Items.Add("Finish");
        _defaultsStrategyDropDown.Items.Add("Both");
        _defaultsStepoverTextBox = new TextBox { PlaceholderText = "%", Width = 100, ToolTip = "Standard-Zustellung in %" };
        _defaultsWidthTextBox = new TextBox { PlaceholderText = "mm", Width = 100, ToolTip = "Standard-Nutbreite in mm" };
        _defaultsDiameterTextBox = new TextBox { PlaceholderText = "mm", Width = 100, ToolTip = "Standard-Bohrdurchmesser in mm" };
        _defaultsPeckCheckBox = new CheckBox { Text = "Peck drilling", TextColor = FgText, ToolTip = "Standard für spänebrechendes Bohren" };
        _defaultsPeckDepthTextBox = new TextBox { PlaceholderText = "mm", Width = 100, ToolTip = "Standard-Peck-Tiefe in mm" };

        var defDepthRow = new TableRow(CreateLabel("Tiefe:", 9, false), new TableCell(_defaultsDepthTextBox, true));
        var defFeedrateRow = new TableRow(CreateLabel("Vorschub:", 9, false), new TableCell(_defaultsFeedrateTextBox, true));
        _defStrategyRow = new TableRow(CreateLabel("Strategie:", 9, false), new TableCell(_defaultsStrategyDropDown, true));
        _defStepoverRow = new TableRow(CreateLabel("Stepover:", 9, false), new TableCell(_defaultsStepoverTextBox, true));
        _defWidthRow = new TableRow(CreateLabel("Breite:", 9, false), new TableCell(_defaultsWidthTextBox, true));
        _defDiameterRow = new TableRow(CreateLabel("Ø Bohrung:", 9, false), new TableCell(_defaultsDiameterTextBox, true));
        _defPeckRow = new TableRow(new TableCell(_defaultsPeckCheckBox) { ScaleWidth = true });
        _defPeckDepthRow = new TableRow(CreateLabel("Peck-Tiefe:", 9, false), new TableCell(_defaultsPeckDepthTextBox, true));

        var defaultsFieldTable = new TableLayout
        {
            Spacing = new Size(8, 4),
            Rows = { defDepthRow, defFeedrateRow, _defStrategyRow, _defStepoverRow, _defWidthRow, _defDiameterRow, _defPeckRow, _defPeckDepthRow }
        };

        var saveDefaultsBtn = new Button { Text = "💾 Speichern", Height = 26, ToolTip = "Angezeigte Werte als neue Standardwerte speichern" };
        saveDefaultsBtn.Click += (_, _) => SaveDefaultsFromFields();
        var resetDefaultsBtn = new Button { Text = "↩ Zurücksetzen", Height = 26, ToolTip = "Maschinenspezifische Standardwerte wiederherstellen" };
        resetDefaultsBtn.Click += (_, _) => ResetDefaults();

        var defaultsBtnRow = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { saveDefaultsBtn, resetDefaultsBtn }
        };

        _defaultsPanel = new StackLayout
        {
            Spacing = 6,
            Items =
            {
                _defaultsTypeDropDown,
                defaultsFieldTable,
                defaultsBtnRow
            }
        };

        var defaultsSection = CreateSection("⚙ Standardwerte", _defaultsPanel, "Vorgabewerte pro Operationstyp", false);

        // --- Statistics Panel ---
        _statsLabel = CreateLabel("—", 9, false);
        _statsLabel.TextColor = FgDim;

        var statsSection = CreateSection("Statistik", _statsLabel, "Bearbeitungsübersicht", false);

        // --- Status Bar ---
        _statusLabel = CreateLabel("Keine Operationen", 9, false);
        _statusLabel.TextColor = FgDim;

        // --- Main Layout ---
        Content = new Scrollable
        {
            Border = BorderType.None,
            Content = new StackLayout
            {
                Padding = new Padding(10),
                Spacing = 8,
                Items =
                {
                    headerLabel,
                    machineRow,
                    toolbar,
                    opsSection,
                    propsSection,
                    defaultsSection,
                    actionButtons,
                    _3dPreviewCheckBox,
                    exportRow,
                    _validationResultLabel,
                    statsSection,
                    _statusLabel
                }
            }
        };

        // Initial state
        ShowPropertiesForType(null);
        LoadMachineProfileFromDocument();
        LoadToolLibrary();
        RefreshOperationsTree();
        UpdateDefaultsDisplay();
        HookDocumentEvents();

        // Wire keyboard shortcuts
        KeyDown += OnPanelKeyDown;
    }

    #region Machine Profile

    private void OnMachineProfileChanged(object? sender, EventArgs e)
    {
        SaveMachineProfileToDocument();
        LoadToolLibrary();
        RefreshOperationsTree();
        UpdateDefaultsDisplay();
        // Re-populate tool dropdown if an operation is selected
        if (_selectedOperation != null)
            ShowPropertiesForOperation(_selectedOperation);
    }

    private IMachineProfile GetCurrentMachineProfile()
    {
        var index = _machineDropDown.SelectedIndex;
        if (index >= 0 && index < MachineProfiles.Length)
            return MachineProfiles[index].Factory();
        return new ScmProfile();
    }

    private string GetCurrentMachineKey()
    {
        var index = _machineDropDown.SelectedIndex;
        if (index >= 0 && index < MachineProfiles.Length)
            return MachineProfiles[index].Key;
        return "xilog";
    }

    private void SaveMachineProfileToDocument()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;
        doc.Strings.SetString(DOC_MACHINE_PROFILE_KEY, GetCurrentMachineKey());
    }

    private void LoadMachineProfileFromDocument()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        var savedKey = doc.Strings.GetValue(DOC_MACHINE_PROFILE_KEY);
        if (string.IsNullOrEmpty(savedKey)) return;

        for (int i = 0; i < MachineProfiles.Length; i++)
        {
            if (string.Equals(MachineProfiles[i].Key, savedKey, StringComparison.OrdinalIgnoreCase))
            {
                _machineDropDown.SelectedIndex = i;
                return;
            }
        }
    }

    #endregion

    #region Keyboard Shortcuts

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.Delete:
            case Keys.Backspace:
                if (_selectedOperation != null)
                {
                    RemoveOperation(_selectedOperation);
                    e.Handled = true;
                }
                break;

            case Keys.F5:
                RefreshOperationsTree();
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Operations Tree

    private TreeGridView CreateOperationsTree()
    {
        var tree = new TreeGridView
        {
            Height = 220,
            AllowMultipleSelection = false,
            ToolTip = "Rechtsklick für Kontextmenü · Doppelklick zum Zoomen · Delete zum Entfernen"
        };

        tree.Columns.Add(new GridColumn
        {
            HeaderText = "Operation",
            Width = 200,
            DataCell = new TextBoxCell(0)
        });
        tree.Columns.Add(new GridColumn
        {
            HeaderText = "Werkzeug",
            Width = 120,
            DataCell = new TextBoxCell(1)
        });
        tree.Columns.Add(new GridColumn
        {
            HeaderText = "Tiefe",
            Width = 60,
            DataCell = new TextBoxCell(2)
        });

        tree.DataStore = new TreeGridItemCollection();

        tree.SelectionChanged += OnTreeSelectionChanged;
        tree.CellDoubleClick += OnTreeCellDoubleClick;

        // Wire context menu via MouseDown
        tree.MouseDown += OnTreeMouseDown;

        return tree;
    }

    private void RefreshOperationsTree()
    {
        _treeItems = new TreeGridItemCollection();

        var doc = RhinoDoc.ActiveDoc;
        if (doc == null)
        {
            _operationsTree.DataStore = _treeItems;
            UpdateStatusBar(0, 0, 0);
            ShowEmptyState(true);
            return;
        }

        List<RhinoObject> operations;
        try
        {
            operations = CncOperationService.GetAllOperationsInDocument(doc).ToList();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CamPanel] Fehler beim Laden der Operationen: {ex.Message}");
            _operationsTree.DataStore = _treeItems;
            UpdateStatusBar(0, 0, 0);
            ShowEmptyState(true);
            return;
        }
        var grouped = operations
            .Select(obj => new { Obj = obj, Op = CncOperationService.GetOperation(obj) })
            .Where(x => x.Op != null)
            .GroupBy(x => x.Op!.Type, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key);

        int totalOps = 0;
        var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int warnings = 0;

        foreach (var group in grouped)
        {
            var groupItem = new TreeGridItem
            {
                Values = new object[] { $"{GetOperationIcon(group.Key)} {group.Key} ({group.Count()})", "", "" }
            };

            foreach (var entry in group)
            {
                var op = entry.Op!;
                var obj = entry.Obj;
                totalOps++;

                var objName = !string.IsNullOrEmpty(obj.Name) ? obj.Name : obj.Attributes.LayerIndex >= 0
                    ? doc.Layers[obj.Attributes.LayerIndex]?.Name ?? $"Object {obj.Id.ToString()[..8]}"
                    : $"Object {obj.Id.ToString()[..8]}";

                var toolName = op.Tool ?? "—";
                var toolDisplay = toolName;
                if (toolName != "—")
                {
                    toolNames.Add(toolName);
                    // Try to show diameter
                    var toolDef = _allTools.FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
                    if (toolDef != null)
                        toolDisplay = $"Ø{toolDef.NominalDiameter:F0} {toolDef.Name}";
                }
                else
                {
                    warnings++;
                }

                var depthStr = op.Depth.HasValue ? $"{op.Depth.Value:F1}mm" : "—";

                var childItem = new TreeGridItem
                {
                    Values = new object[] { $"  {objName}", toolDisplay, depthStr },
                    Tag = new OperationEntry(obj.Id, op, objName)
                };

                groupItem.Children.Add(childItem);
            }

            groupItem.Expanded = true;
            _treeItems.Add(groupItem);
        }

        _operationsTree.DataStore = _treeItems;
        UpdateStatusBar(totalOps, toolNames.Count, warnings);
        ShowEmptyState(totalOps == 0);
        UpdateStatistics();
    }

    private void ShowEmptyState(bool show)
    {
        _emptyStateLabel.Visible = show;
        _operationsTree.Visible = !show;
    }

    private void OnTreeSelectionChanged(object? sender, EventArgs e)
    {
        var selectedItem = _operationsTree.SelectedItem as TreeGridItem;
        if (selectedItem?.Tag is OperationEntry entry)
        {
            _selectedOperation = entry;
            ShowPropertiesForOperation(entry);

            // Select object in viewport
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                doc.Objects.UnselectAll();
                doc.Objects.Select(entry.ObjectId, true);
                doc.Views.Redraw();
            }
        }
        else
        {
            _selectedOperation = null;
            ShowPropertiesForType(null);
        }
    }

    private void OnTreeCellDoubleClick(object? sender, GridCellMouseEventArgs e)
    {
        var item = e.Item as TreeGridItem;
        if (item?.Tag is OperationEntry entry)
        {
            ZoomToOperation(entry);
        }
    }

    private void OnTreeMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Buttons != MouseButtons.Alternate) return; // Right-click only

        // Try to find the item under the mouse
        var selectedItem = _operationsTree.SelectedItem as TreeGridItem;
        if (selectedItem?.Tag is OperationEntry entry)
        {
            var menu = CreateOperationContextMenu(entry);
            menu.Show(_operationsTree);
        }
    }

    #endregion

    #region Context Menu

    private ContextMenu CreateOperationContextMenu(OperationEntry entry)
    {
        var menu = new ContextMenu();

        var editItem = new ButtonMenuItem { Text = "✏️ Bearbeiten…" };
        editItem.Click += (_, _) => OpenEditDialog(entry);
        menu.Items.Add(editItem);

        menu.Items.Add(new SeparatorMenuItem());

        var removeItem = new ButtonMenuItem { Text = "🗑 Entfernen" };
        removeItem.Click += (_, _) => RemoveOperation(entry);
        menu.Items.Add(removeItem);

        var regenItem = new ButtonMenuItem { Text = "🔄 Toolpath neu generieren" };
        regenItem.Click += (_, _) =>
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var obj = doc.Objects.FindId(entry.ObjectId);
            if (obj == null) return;
            var toolDiam = entry.Operation.Diameter ?? GetToolDiameterByName(entry.Operation.Tool);
            RegenerateToolpath(doc, obj, entry.Operation.Type, toolDiam);
            doc.Views.Redraw();
            RhinoApp.WriteLine($"[CamPanel] Toolpath regeneriert für {entry.ObjectName}");
        };
        menu.Items.Add(regenItem);

        menu.Items.Add(new SeparatorMenuItem());

        var selectItem = new ButtonMenuItem { Text = "🎯 Im Viewport selektieren" };
        selectItem.Click += (_, _) =>
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            doc.Objects.UnselectAll();
            doc.Objects.Select(entry.ObjectId, true);
            doc.Views.Redraw();
        };
        menu.Items.Add(selectItem);

        var zoomItem = new ButtonMenuItem { Text = "🔍 Zoom auf Objekt" };
        zoomItem.Click += (_, _) => ZoomToOperation(entry);
        menu.Items.Add(zoomItem);

        return menu;
    }

    private void OpenEditDialog(OperationEntry entry)
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        var obj = doc.Objects.FindId(entry.ObjectId);
        if (obj == null)
        {
            RhinoApp.WriteLine("[CamPanel] Objekt nicht gefunden — möglicherweise gelöscht.");
            return;
        }

        try
        {
            var op = entry.Operation;
            var profile = GetCurrentMachineProfile();
            var library = _toolLibraryStore.LoadOrCreate(profile);

            var machineKey = GetCurrentMachineKey();
            var opDefaults = OperationDefaults.GetDefaults(op.Type, machineKey);

            CamOperationDialogBase? dialog = op.Type.ToUpperInvariant() switch
            {
                "CONTOUR" => new ContourOperationDialog(_toolLibraryStore, library, opDefaults),
                "POCKET" => new PocketOperationDialog(_toolLibraryStore, library, opDefaults),
                "DRILL" => new DrillOperationDialog(_toolLibraryStore, library, opDefaults),
                "GROOVE" => new GrooveOperationDialog(_toolLibraryStore, library, opDefaults),
                _ => null
            };

            if (dialog == null)
            {
                RhinoApp.WriteLine($"[CamPanel] Unbekannter Operationstyp: {op.Type}");
                return;
            }

            // Pre-fill dialog with current values
            dialog.PreFill(op);

            var result = dialog.ShowModalOnTop();
            if (result == null) return;

            var undoSerial = doc.BeginUndoRecord($"CNC Edit {op.Type}");

            try
            {
                CncOperationService.SetOperation(obj, op.Type, result);

                var toolName = result.TryGetValue(CncOperationSchema.CNC_TOOL, out var tn) ? tn?.ToString() : null;
                var toolDiam = GetToolDiameterByName(toolName);
                if (result.TryGetValue(CncOperationSchema.CNC_DIAMETER, out var diamObj) && diamObj is double d)
                    toolDiam = d;

                RegenerateToolpath(doc, obj, op.Type, toolDiam);
                doc.EndUndoRecord(undoSerial);
            }
            catch
            {
                doc.EndUndoRecord(undoSerial);
                throw;
            }

            doc.Views.Redraw();
            RefreshOperationsTree();
            RhinoApp.WriteLine($"[CamPanel] {op.Type}-Operation auf {entry.ObjectName} aktualisiert.");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CamPanel] Fehler beim Bearbeiten: {ex.Message}");
        }
    }

    private void ZoomToOperation(OperationEntry entry)
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        var obj = doc.Objects.FindId(entry.ObjectId);
        if (obj == null) return;

        doc.Objects.UnselectAll();
        doc.Objects.Select(entry.ObjectId, true);
        var bbox = obj.Geometry.GetBoundingBox(true);
        // Inflate bbox slightly for nicer framing
        bbox.Inflate(bbox.Diagonal.Length * 0.1);
        doc.Views.ActiveView?.ActiveViewport.ZoomBoundingBox(bbox);
        doc.Views.Redraw();
    }

    private void RemoveOperation(OperationEntry entry)
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        var obj = doc.Objects.FindId(entry.ObjectId);
        if (obj == null)
        {
            RhinoApp.WriteLine("[CamPanel] Objekt nicht gefunden — möglicherweise bereits gelöscht.");
            RefreshOperationsTree();
            return;
        }

        var undoSerial = doc.BeginUndoRecord("CNC Remove Operation");

        try
        {
            // Remove toolpath geometry (2D + 3D)
            ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);
            ToolpathVisualizer.RemoveToolpath3DGeometry(doc, obj);
            // Remove operation UserText
            CncOperationService.RemoveOperation(obj);

            if (EdgeCurveHelper.IsExtractedEdgeCurve(obj))
            {
                doc.Objects.Delete(obj.Id, true);
            }
            else
            {
                CncOperationService.RestoreDefaultColor(obj);
            }

            doc.EndUndoRecord(undoSerial);
        }
        catch (Exception ex)
        {
            doc.EndUndoRecord(undoSerial);
            RhinoApp.WriteLine($"[CamPanel] Fehler beim Entfernen: {ex.Message}");
        }

        doc.Views.Redraw();
        _selectedOperation = null;
        ShowPropertiesForType(null);
        RefreshOperationsTree();

        RhinoApp.WriteLine($"[CamPanel] {entry.Operation.Type} von {entry.ObjectName} entfernt.");
    }

    #endregion

    #region Properties Panel

    private void ShowPropertiesForType(string? operationType)
    {
        // Hide all conditional rows
        SetRowVisibility(_strategyRow, false);
        SetRowVisibility(_widthRow, false);
        SetRowVisibility(_diameterRow, false);
        SetRowVisibility(_stepoverRow, false);
        SetRowVisibility(_peckRow, false);
        SetRowVisibility(_peckDepthRow, false);
        SetRowVisibility(_rampEntryRow, false);

        if (operationType == null)
        {
            _propTypeLabel.Text = "Keine Auswahl";
            _propObjectLabel.Text = "Wähle eine Operation im Baum oben aus";
            _propertiesPanel.Enabled = false;
            return;
        }

        _propertiesPanel.Enabled = true;

        // Show type-specific rows
        switch (operationType.ToUpperInvariant())
        {
            case "CONTOUR":
                SetRowVisibility(_strategyRow, true);
                PopulateToolDropDown(ToolKind.Router);
                break;
            case "POCKET":
                SetRowVisibility(_stepoverRow, true);
                SetRowVisibility(_rampEntryRow, true);
                PopulateToolDropDown(ToolKind.Router);
                break;
            case "DRILL":
                SetRowVisibility(_diameterRow, true);
                SetRowVisibility(_peckRow, true);
                SetRowVisibility(_peckDepthRow, true);
                PopulateToolDropDown(ToolKind.Drill);
                break;
            case "GROOVE":
                SetRowVisibility(_widthRow, true);
                SetRowVisibility(_strategyRow, true);
                PopulateToolDropDown(ToolKind.Router);
                break;
        }
    }

    private void ShowPropertiesForOperation(OperationEntry entry)
    {
        var op = entry.Operation;
        var type = op.Type;

        _propTypeLabel.Text = $"{GetOperationIcon(type)} {type}";
        _propObjectLabel.Text = entry.ObjectName;

        ShowPropertiesForType(type);

        // Populate fields
        _propDepthTextBox.Text = op.Depth?.ToString("F1", CultureInfo.InvariantCulture) ?? "";

        // Select current tool in dropdown
        SelectToolInDropDown(op.Tool);

        switch (type.ToUpperInvariant())
        {
            case "CONTOUR":
                SetStrategyDropDown(op.Strategy);
                break;
            case "POCKET":
                _propStepoverTextBox.Text = op.Stepover?.ToString("F0", CultureInfo.InvariantCulture) ?? "50";
                SetRampEntryDropDown(op.RampEntry);
                break;
            case "DRILL":
                _propDiameterTextBox.Text = op.Diameter?.ToString("F1", CultureInfo.InvariantCulture) ?? "";
                _propPeckCheckBox.Checked = op.Peck ?? false;
                _propPeckDepthTextBox.Text = op.PeckDepth?.ToString("F1", CultureInfo.InvariantCulture) ?? "";
                break;
            case "GROOVE":
                _propWidthTextBox.Text = op.Width?.ToString("F1", CultureInfo.InvariantCulture) ?? "";
                SetStrategyDropDown(op.Strategy);
                break;
        }
    }

    private void ApplyPropertyChanges()
    {
        if (_selectedOperation == null) return;

        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        var obj = doc.Objects.FindId(_selectedOperation.ObjectId);
        if (obj == null)
        {
            RhinoApp.WriteLine("[CamPanel] Objekt nicht gefunden — möglicherweise gelöscht.");
            return;
        }

        var undoSerial = doc.BeginUndoRecord("CNC Apply Properties");

        try
        {
        var op = _selectedOperation.Operation;
        var parameters = new Dictionary<string, object>();

        // Tool
        var selectedTool = GetSelectedTool();
        if (selectedTool != null)
        {
            parameters[CncOperationSchema.CNC_TOOL] = selectedTool.Name;
            parameters[CncOperationSchema.CNC_DIAMETER] = selectedTool.NominalDiameter;
        }

        // Depth
        if (double.TryParse(_propDepthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var depth))
            parameters[CncOperationSchema.CNC_DEPTH] = depth;

        // Type-specific parameters
        switch (op.Type.ToUpperInvariant())
        {
            case "CONTOUR":
                parameters[CncOperationSchema.CNC_STRATEGY] = GetSelectedStrategy();
                break;
            case "POCKET":
                if (double.TryParse(_propStepoverTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var stepover))
                    parameters[CncOperationSchema.CNC_STEPOVER] = stepover;
                parameters[CncOperationSchema.CNC_RAMP_ENTRY] = GetSelectedRampEntry();
                break;
            case "DRILL":
                if (double.TryParse(_propDiameterTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var diam))
                    parameters[CncOperationSchema.CNC_DIAMETER] = diam;
                parameters[CncOperationSchema.CNC_PECK] = _propPeckCheckBox.Checked ?? false;
                if (double.TryParse(_propPeckDepthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var peckDepth))
                    parameters[CncOperationSchema.CNC_PECK_DEPTH] = peckDepth;
                break;
            case "GROOVE":
                if (double.TryParse(_propWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
                    parameters[CncOperationSchema.CNC_WIDTH] = width;
                parameters[CncOperationSchema.CNC_STRATEGY] = GetSelectedStrategy();
                break;
        }

        // Apply changes
        CncOperationService.SetOperation(obj, op.Type, parameters);

        // Regenerate toolpath for this operation
        RegenerateToolpath(doc, obj, op.Type, selectedTool?.NominalDiameter ?? 0);

        doc.EndUndoRecord(undoSerial);
        doc.Views.Redraw();
        RefreshOperationsTree();

        RhinoApp.WriteLine($"[CamPanel] {op.Type}-Operation auf {_selectedOperation.ObjectName} aktualisiert.");
        }
        catch (Exception ex)
        {
            doc.EndUndoRecord(undoSerial);
            RhinoApp.WriteLine($"[CamPanel] Fehler beim Anwenden der Eigenschaften: {ex.Message}");
        }
    }

    #endregion

    #region Toolpath Generation

    private void GenerateAllToolpaths()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        try
        {
            var operations = CncOperationService.GetAllOperationsInDocument(doc).ToList();
            int generated = 0;
            int errors = 0;
            var is3D = _3dPreviewCheckBox.Checked ?? false;

            foreach (var obj in operations)
            {
                try
                {
                    var op = CncOperationService.GetOperation(obj);
                    if (op == null) continue;

                    var toolDiam = op.Diameter ?? GetToolDiameterByName(op.Tool);
                    if (toolDiam <= 0) continue;

                    // Remove existing toolpaths first
                    ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);
                    ToolpathVisualizer.RemoveToolpath3DGeometry(doc, obj);

                    // Regenerate 2D
                    RegenerateToolpath(doc, obj, op.Type, toolDiam);

                    // Regenerate 3D if enabled
                    if (is3D)
                    {
                        var depth = op.Depth ?? 0;
                        var geometry3D = Generate3DToolpath(obj, op.Type, toolDiam, depth);
                        if (geometry3D.Count > 0)
                            ToolpathVisualizer.AddToolpath3DToDocument(doc, obj, op.Type, geometry3D);
                    }

                    generated++;
                }
                catch (Exception ex)
                {
                    errors++;
                    RhinoApp.WriteLine($"[CamPanel] Fehler bei Toolpath-Generierung für {obj.Id}: {ex.Message}");
                }
            }

            doc.Views.Redraw();
            var errMsg = errors > 0 ? $", {errors} Fehler" : "";
            RhinoApp.WriteLine($"[CamPanel] Toolpaths für {generated} Operation(en) generiert{(is3D ? " (2D+3D)" : "")}{errMsg}.");
            RefreshOperationsTree();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CamPanel] Fehler bei Toolpath-Generierung: {ex.Message}");
        }
    }

    private void ClearAllToolpaths()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        try
        {
            var operations = CncOperationService.GetAllOperationsInDocument(doc).ToList();
            int cleared = 0;

            foreach (var obj in operations)
            {
                try
                {
                    ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);
                    ToolpathVisualizer.RemoveToolpath3DGeometry(doc, obj);
                    cleared++;
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"[CamPanel] Fehler beim Entfernen von Toolpath für {obj.Id}: {ex.Message}");
                }
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"[CamPanel] Toolpaths für {cleared} Operation(en) gelöscht.");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CamPanel] Fehler beim Löschen der Toolpaths: {ex.Message}");
        }
    }

    private void RegenerateToolpath(RhinoDoc doc, RhinoObject obj, string operationType, double toolDiameter)
    {
        // Remove existing toolpath first
        ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);

        if (toolDiameter <= 0) return;

        var geometry = obj.Geometry;
        List<GeometryBase> toolpathGeometry;

        switch (operationType.ToUpperInvariant())
        {
            case "CONTOUR":
            case "GROOVE":
                if (geometry is Curve curve)
                {
                    toolpathGeometry = ToolpathVisualizer.CreateContourToolpath(curve, toolDiameter);
                    ToolpathVisualizer.AddToolpathToDocument(doc, obj, operationType, toolpathGeometry);
                }
                break;
            case "POCKET":
                if (geometry is Curve pocketCurve)
                {
                    var stepover = GetStepoverFromObject(obj);
                    toolpathGeometry = ToolpathVisualizer.CreatePocketToolpath(pocketCurve, toolDiameter, stepover);
                    ToolpathVisualizer.AddToolpathToDocument(doc, obj, operationType, toolpathGeometry);
                }
                break;
            case "DRILL":
                if (geometry is Rhino.Geometry.Point point)
                {
                    toolpathGeometry = ToolpathVisualizer.CreateDrillToolpath(point.Location, toolDiameter);
                    ToolpathVisualizer.AddToolpathToDocument(doc, obj, operationType, toolpathGeometry);
                }
                else if (geometry is Curve drillCurve)
                {
                    // Drill points stored as small circles
                    var center = drillCurve.PointAtStart;
                    toolpathGeometry = ToolpathVisualizer.CreateDrillToolpath(center, toolDiameter);
                    ToolpathVisualizer.AddToolpathToDocument(doc, obj, operationType, toolpathGeometry);
                }
                break;
        }
    }

    private double GetStepoverFromObject(RhinoObject obj)
    {
        var stepoverStr = obj.Attributes.GetUserString(CncOperationSchema.CNC_STEPOVER);
        if (double.TryParse(stepoverStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return val;
        return 50.0; // Default 50%
    }

    #endregion

    #region Tool Library

    private void LoadToolLibrary()
    {
        try
        {
            var profile = GetCurrentMachineProfile();
            var library = _toolLibraryStore.LoadOrCreate(profile);
            _allTools = library.Tools.ToList();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CamPanel] Werkzeugbibliothek konnte nicht geladen werden: {ex.Message}");
            _allTools = new List<ToolDefinition>();
        }
    }

    private void PopulateToolDropDown(ToolKind kind)
    {
        _propToolDropDown.Items.Clear();
        var filtered = _allTools
            .Where(t => t.Kind == kind)
            .OrderBy(t => t.NominalDiameter)
            .ThenBy(t => t.Name);

        foreach (var tool in filtered)
        {
            var fluteInfo = tool.FluteCount.HasValue ? $" ({tool.FluteCount}-Schneider)" : "";
            _propToolDropDown.Items.Add(new ListItem
            {
                Text = $"Ø{tool.NominalDiameter:F1} {tool.Name}{fluteInfo}",
                Key = tool.Id
            });
        }

        // "Manage Tools..." option at the bottom
        if (_propToolDropDown.Items.Count > 0)
        {
            _propToolDropDown.Items.Add(new ListItem { Text = "── Werkzeuge verwalten… ──", Key = "__manage__" });
        }
        else
        {
            _propToolDropDown.Items.Add(new ListItem { Text = "⚠ Keine Werkzeuge für diesen Typ", Key = "__none__" });
            _propToolDropDown.Items.Add(new ListItem { Text = "── Werkzeuge verwalten… ──", Key = "__manage__" });
        }

        _propToolDropDown.SelectedIndexChanged -= OnToolDropDownChanged;
        if (_propToolDropDown.Items.Count > 0)
            _propToolDropDown.SelectedIndex = 0;
        _propToolDropDown.SelectedIndexChanged += OnToolDropDownChanged;
    }

    private void OnToolDropDownChanged(object? sender, EventArgs e)
    {
        if (_propToolDropDown.SelectedIndex < 0) return;

        var selectedItem = _propToolDropDown.Items[_propToolDropDown.SelectedIndex] as ListItem;
        if (selectedItem?.Key == "__manage__")
        {
            // Open tool library manager
            _propToolDropDown.SelectedIndex = Math.Max(0, _propToolDropDown.SelectedIndex - 1);
            OpenToolLibraryManager();
        }
    }

    private void OpenToolLibraryManager()
    {
        try
        {
            var profile = GetCurrentMachineProfile();
            var library = _toolLibraryStore.LoadOrCreate(profile);
            var dialog = new ToolLibraryManagerDialog(library);
            var result = dialog.ShowModal(this);
            if (result == null) return;

            _toolLibraryStore.Save(profile, result);
            _allTools = result.Tools.ToList();
            RhinoApp.WriteLine($"[CamPanel] Werkzeugbibliothek gespeichert: {result.Tools.Count} Werkzeuge");

            // Re-populate tool dropdown for current operation
            if (_selectedOperation != null)
                ShowPropertiesForOperation(_selectedOperation);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CamPanel] Werkzeugmanager-Fehler: {ex.Message}");
        }
    }

    private void SelectToolInDropDown(string? toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return;

        for (int i = 0; i < _propToolDropDown.Items.Count; i++)
        {
            var item = _propToolDropDown.Items[i] as ListItem;
            if (item?.Key == "__manage__" || item?.Key == "__none__") continue;
            if (_propToolDropDown.Items[i].Text.Contains(toolName, StringComparison.OrdinalIgnoreCase))
            {
                _propToolDropDown.SelectedIndexChanged -= OnToolDropDownChanged;
                _propToolDropDown.SelectedIndex = i;
                _propToolDropDown.SelectedIndexChanged += OnToolDropDownChanged;
                return;
            }
        }
    }

    private ToolDefinition? GetSelectedTool()
    {
        if (_propToolDropDown.SelectedIndex < 0) return null;
        var selectedKey = (_propToolDropDown.Items[_propToolDropDown.SelectedIndex] as ListItem)?.Key;
        if (selectedKey == "__manage__" || selectedKey == "__none__") return null;
        return _allTools.FirstOrDefault(t => t.Id == selectedKey);
    }

    private double GetToolDiameterByName(string? toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return 0;
        var tool = _allTools.FirstOrDefault(t =>
            t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
        return tool?.NominalDiameter ?? 0;
    }

    #endregion

    #region Document Events (Auto-Refresh)

    private void HookDocumentEvents()
    {
        if (_eventsHooked) return;

        RhinoDoc.ActiveDocumentChanged += OnActiveDocumentChanged;
        RhinoDoc.CloseDocument += OnDocumentClosed;

        HookObjectEvents();
        _eventsHooked = true;
    }

    private void HookObjectEvents()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        _lastDocSerialNumber = doc.RuntimeSerialNumber;

        // Use static events
        RhinoDoc.AddRhinoObject += OnObjectChanged;
        RhinoDoc.DeleteRhinoObject += OnObjectChanged;
        RhinoDoc.ModifyObjectAttributes += OnObjectAttributesChanged;
    }

    private void UnhookObjectEvents()
    {
        RhinoDoc.AddRhinoObject -= OnObjectChanged;
        RhinoDoc.DeleteRhinoObject -= OnObjectChanged;
        RhinoDoc.ModifyObjectAttributes -= OnObjectAttributesChanged;
    }

    private void OnActiveDocumentChanged(object? sender, DocumentEventArgs e)
    {
        UnhookObjectEvents();
        HookObjectEvents();
        LoadMachineProfileFromDocument();
        LoadToolLibrary();

        // Schedule refresh on UI thread
        Application.Instance.AsyncInvoke(() => RefreshOperationsTree());
    }

    private void OnDocumentClosed(object? sender, DocumentEventArgs e)
    {
        Application.Instance.AsyncInvoke(() =>
        {
            _treeItems = new TreeGridItemCollection();
            _operationsTree.DataStore = _treeItems;
            UpdateStatusBar(0, 0, 0);
            ShowEmptyState(true);
            ShowPropertiesForType(null);
        });
    }

    private void OnObjectChanged(object? sender, RhinoObjectEventArgs e)
    {
        // Debounce: only refresh if this is from the active doc
        if (e.TheObject?.Document?.RuntimeSerialNumber != _lastDocSerialNumber) return;

        Application.Instance.AsyncInvoke(() => RefreshOperationsTree());
    }

    private void OnObjectAttributesChanged(object? sender, RhinoModifyObjectAttributesEventArgs e)
    {
        if (e.RhinoObject?.Document?.RuntimeSerialNumber != _lastDocSerialNumber) return;

        Application.Instance.AsyncInvoke(() => RefreshOperationsTree());
    }

    #endregion

    #region Validation

    private void RunValidation()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null)
        {
            ShowValidationResult(null, "Kein aktives Dokument.");
            return;
        }

        var format = GetCurrentMachineKey() switch
        {
            "biesse" => MachineFormat.Biesse,
            "maestrocadt" => MachineFormat.Xilog,
            _ => MachineFormat.Xilog
        };

        var result = CamValidator.Validate(doc, _allTools, format);

        ShowValidationResult(result, null);

        // Select/highlight objects with errors in viewport
        if (result.HasErrors || result.HasWarnings)
        {
            doc.Objects.UnselectAll();
            foreach (var issue in result.Issues)
            {
                if (issue.ObjectId.HasValue)
                    doc.Objects.Select(issue.ObjectId.Value, true);
            }
            doc.Views.Redraw();
        }

        // Log all issues to Rhino output
        foreach (var issue in result.Issues)
        {
            var prefix = issue.Severity switch
            {
                Severity.Error => "❌",
                Severity.Warning => "⚠️",
                _ => "ℹ️"
            };
            RhinoApp.WriteLine($"[Validierung] {prefix} [{issue.Category}] {issue.Message}");
        }

        if (result.IsClean)
            RhinoApp.WriteLine("[Validierung] ✅ Alle Prüfungen bestanden — Export möglich.");
    }

    private void ShowValidationResult(ValidationResult? result, string? customMessage)
    {
        if (result == null && customMessage != null)
        {
            _validationResultLabel.Text = $"❌ {customMessage}";
            _validationResultLabel.TextColor = Eto.Drawing.Color.FromArgb(220, 60, 60);
            _validationResultLabel.Visible = true;
            return;
        }

        if (result == null || result.IsClean)
        {
            _validationResultLabel.Text = "✅ Validierung bestanden — keine Probleme gefunden.";
            _validationResultLabel.TextColor = Eto.Drawing.Color.FromArgb(60, 180, 80);
            _validationResultLabel.Visible = true;
            return;
        }

        var lines = new List<string>();

        if (result.HasErrors)
        {
            _validationResultLabel.TextColor = Eto.Drawing.Color.FromArgb(220, 60, 60);
            lines.Add($"❌ {result.FormatSummary()} — Export blockiert.");
        }
        else if (result.HasWarnings)
        {
            _validationResultLabel.TextColor = Eto.Drawing.Color.FromArgb(220, 200, 40);
            lines.Add($"⚠ {result.FormatSummary()} — Export möglich.");
        }

        // Show first few issues inline
        var shownCount = 0;
        foreach (var issue in result.Issues.OrderByDescending(i => i.Severity))
        {
            if (shownCount >= 5) { lines.Add("… weitere Probleme in Rhino-Ausgabe"); break; }
            var icon = issue.Severity == Severity.Error ? "❌" : issue.Severity == Severity.Warning ? "⚠" : "ℹ";
            lines.Add($"  {icon} {issue.Message}");
            shownCount++;
        }

        _validationResultLabel.Text = string.Join("\n", lines);
        _validationResultLabel.Visible = true;
    }

    #endregion

    #region Export & 3D Preview

    private void ExportInteractiveCnc()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null)
        {
            RhinoApp.WriteLine("[CamPanel] ❌ Kein aktives Rhino-Dokument.");
            return;
        }

        // Pre-export validation
        var exportFormat = GetCurrentMachineKey() switch
        {
            "biesse" => MachineFormat.Biesse,
            _ => MachineFormat.Xilog
        };
        var validationResult = CamValidator.Validate(doc, _allTools, exportFormat);
        ShowValidationResult(validationResult, null);

        if (validationResult.HasErrors)
        {
            RhinoApp.WriteLine("[CamPanel] ❌ Export abgebrochen — Validierungsfehler vorhanden. Bitte zuerst beheben.");
            doc.Objects.UnselectAll();
            foreach (var issue in validationResult.Issues.Where(i => i.Severity == Severity.Error && i.ObjectId.HasValue))
                doc.Objects.Select(issue.ObjectId!.Value, true);
            doc.Views.Redraw();
            return;
        }

        var operations = _exportBridge.CollectOperations(doc);
        if (operations.Count == 0)
        {
            RhinoApp.WriteLine("[CamPanel] ❌ Keine interaktiven CNC-Operationen gefunden.");
            return;
        }

        // Determine format from machine profile
        var profile = GetCurrentMachineProfile();
        var format = GetCurrentMachineKey() switch
        {
            "biesse" => MachineFormat.Biesse,
            _ => MachineFormat.Xilog
        };

        // Generate CNC code preview (without writing to file)
        var previews = _exportBridge.GenerateCode(doc, format, profile);
        if (previews.Count == 0)
        {
            RhinoApp.WriteLine("[CamPanel] ❌ Code-Generierung fehlgeschlagen — keine Platten erzeugt.");
            return;
        }

        // Show ExportPreviewDialog
        var previewDialog = new ExportPreviewDialog(previews);
        var confirmed = previewDialog.ShowModalOnTop();

        if (!confirmed)
        {
            RhinoApp.WriteLine("[CamPanel] Export abgebrochen durch Benutzer.");
            return;
        }

        // User confirmed → choose output path and write
        var extension = format switch
        {
            MachineFormat.Biesse => ".cix",
            _ => ".xcs"
        };

        var defaultName = string.IsNullOrWhiteSpace(doc.Name)
            ? "program" + extension
            : Path.ChangeExtension(doc.Name, extension);

        string outputPath;

        if (previews.Count > 1)
        {
            var folderDialog = new SelectFolderDialog
            {
                Title = $"Export-Ordner wählen ({previews.Count} Platten)"
            };

            if (folderDialog.ShowDialog(this) != DialogResult.Ok || string.IsNullOrWhiteSpace(folderDialog.Directory))
                return;

            outputPath = folderDialog.Directory;
        }
        else
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "CNC-Programm exportieren",
                FileName = defaultName
            };
            saveDialog.Filters.Add(new FileFilter($"CNC Datei (*{extension})", extension));

            if (saveDialog.ShowDialog(this) != DialogResult.Ok || string.IsNullOrWhiteSpace(saveDialog.FileName))
                return;

            outputPath = saveDialog.FileName;
        }

        try
        {
            var result = _exportBridge.Export(doc, outputPath, format, profile);

            if (result.Success)
            {
                foreach (var file in result.ExportedFiles)
                {
                    RhinoApp.WriteLine($"[CamPanel] ✅ CNC erstellt: {file}");
                }
                RhinoApp.WriteLine($"[CamPanel] Export erfolgreich: {result.OperationCount} Op., {result.PlateCount} Platte(n), {result.ExportedFiles.Count} Datei(en)");
            }
            else
            {
                RhinoApp.WriteLine($"[CamPanel] ❌ Export fehlgeschlagen: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CamPanel] ❌ Export-Fehler: {ex.Message}");
        }
    }

    private void OnToggle3DPreview()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        try
        {
            var is3D = _3dPreviewCheckBox.Checked ?? false;
            var operations = CncOperationService.GetAllOperationsInDocument(doc).ToList();

            foreach (var obj in operations)
            {
                try
                {
                    var op = CncOperationService.GetOperation(obj);
                    if (op == null) continue;

                    var toolDiam = op.Diameter ?? GetToolDiameterByName(op.Tool);
                    if (toolDiam <= 0) continue;

                    var depth = op.Depth ?? 0;

                    if (is3D)
                    {
                        ToolpathVisualizer.RemoveToolpath3DGeometry(doc, obj);
                        var geometry3D = Generate3DToolpath(obj, op.Type, toolDiam, depth);
                        if (geometry3D.Count > 0)
                            ToolpathVisualizer.AddToolpath3DToDocument(doc, obj, op.Type, geometry3D);
                    }
                    else
                    {
                        ToolpathVisualizer.RemoveToolpath3DGeometry(doc, obj);
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"[CamPanel] 3D-Vorschau-Fehler für {obj.Id}: {ex.Message}");
                }
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"[CamPanel] 3D-Vorschau {(is3D ? "aktiviert" : "deaktiviert")} für {operations.Count} Operation(en).");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CamPanel] 3D-Vorschau-Fehler: {ex.Message}");
        }
    }

    private List<GeometryBase> Generate3DToolpath(RhinoObject obj, string operationType, double toolDiameter, double depth)
    {
        var geometry = obj.Geometry;

        switch (operationType.ToUpperInvariant())
        {
            case "CONTOUR":
            case "GROOVE":
                if (geometry is Curve curve)
                    return ToolpathVisualizer.CreateContourToolpath3D(curve, toolDiameter, depth);
                break;
            case "POCKET":
                if (geometry is Curve pocketCurve)
                {
                    var stepover = GetStepoverFromObject(obj);
                    return ToolpathVisualizer.CreatePocketToolpath3D(pocketCurve, toolDiameter, stepover, depth);
                }
                break;
            case "DRILL":
                Point3d center;
                if (geometry is Rhino.Geometry.Point point)
                    center = point.Location;
                else if (geometry is Curve drillCurve)
                    center = drillCurve.PointAtStart;
                else
                    return new List<GeometryBase>();
                return ToolpathVisualizer.CreateDrillToolpath3D(center, toolDiameter, depth);
        }

        return new List<GeometryBase>();
    }

    private void UpdateStatistics()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null)
        {
            _statsLabel.Text = "—";
            return;
        }

        var stats = InteractiveExportBridge.GetStatistics(doc, _allTools);
        if (stats.TotalOperations == 0)
        {
            _statsLabel.Text = "Keine Operationen";
            return;
        }

        _statsLabel.Text = stats.FormatSummary();
    }

    #endregion

    #region Defaults Management

    /// <summary>
    /// Updates the defaults editor fields with current values for the selected operation type.
    /// </summary>
    private void UpdateDefaultsDisplay()
    {
        var machineKey = GetCurrentMachineKey();
        var typeKey = GetSelectedDefaultsType();

        var defaults = OperationDefaults.GetDefaults(typeKey, machineKey);

        // Populate common fields
        _defaultsDepthTextBox.Text = defaults.Depth.ToString("F1", CultureInfo.InvariantCulture);
        _defaultsFeedrateTextBox.Text = defaults.Feedrate.ToString("F0", CultureInfo.InvariantCulture);

        // Show/hide type-specific fields
        var isContour = typeKey.Equals("Contour", StringComparison.OrdinalIgnoreCase);
        var isPocket = typeKey.Equals("Pocket", StringComparison.OrdinalIgnoreCase);
        var isDrill = typeKey.Equals("Drill", StringComparison.OrdinalIgnoreCase);
        var isGroove = typeKey.Equals("Groove", StringComparison.OrdinalIgnoreCase);

        SetRowVisibility(_defStrategyRow, isContour || isPocket || isGroove);
        SetRowVisibility(_defStepoverRow, isPocket);
        SetRowVisibility(_defWidthRow, isGroove);
        SetRowVisibility(_defDiameterRow, isDrill);
        SetRowVisibility(_defPeckRow, isDrill);
        SetRowVisibility(_defPeckDepthRow, isDrill);

        // Strategy
        if (isContour || isPocket || isGroove)
        {
            _defaultsStrategyDropDown.SelectedIndex = defaults.Strategy?.ToUpperInvariant() switch
            {
                "FINISH" => 1,
                "BOTH" => 2,
                _ => 0
            };
        }

        // Pocket-specific
        if (isPocket)
        {
            _defaultsStepoverTextBox.Text = defaults.Stepover.ToString("F0", CultureInfo.InvariantCulture);
        }

        // Drill-specific
        if (isDrill)
        {
            _defaultsDiameterTextBox.Text = defaults.Diameter.ToString("F1", CultureInfo.InvariantCulture);
            _defaultsPeckCheckBox.Checked = defaults.Peck;
            _defaultsPeckDepthTextBox.Text = defaults.PeckDepth.ToString("F1", CultureInfo.InvariantCulture);
        }

        // Groove-specific
        if (isGroove)
        {
            _defaultsWidthTextBox.Text = defaults.Width.ToString("F1", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Called when the defaults operation type dropdown changes.
    /// </summary>
    private void OnDefaultsTypeChanged()
    {
        UpdateDefaultsDisplay();
    }

    /// <summary>
    /// Gets the currently selected operation type key from the defaults dropdown.
    /// </summary>
    private string GetSelectedDefaultsType()
    {
        if (_defaultsTypeDropDown.SelectedIndex < 0) return "Contour";
        var item = _defaultsTypeDropDown.Items[_defaultsTypeDropDown.SelectedIndex] as ListItem;
        return item?.Key ?? "Contour";
    }

    /// <summary>
    /// Saves the currently displayed defaults field values for the selected operation type.
    /// </summary>
    private void SaveDefaultsFromFields()
    {
        var typeKey = GetSelectedDefaultsType();
        var values = new OperationDefaultValues();

        // Common fields
        if (double.TryParse(_defaultsDepthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var depth))
            values.Depth = depth;
        if (double.TryParse(_defaultsFeedrateTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var feedrate))
            values.Feedrate = feedrate;

        // Type-specific fields
        switch (typeKey.ToUpperInvariant())
        {
            case "CONTOUR":
            case "GROOVE":
                values.Strategy = _defaultsStrategyDropDown.SelectedIndex switch
                {
                    1 => CncOperationSchema.STRATEGY_FINISH,
                    2 => CncOperationSchema.STRATEGY_BOTH,
                    _ => CncOperationSchema.STRATEGY_ROUGH
                };
                if (typeKey.Equals("Groove", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(_defaultsWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
                        values.Width = width;
                }
                break;
            case "POCKET":
                values.Strategy = _defaultsStrategyDropDown.SelectedIndex switch
                {
                    1 => CncOperationSchema.STRATEGY_FINISH,
                    2 => CncOperationSchema.STRATEGY_BOTH,
                    _ => CncOperationSchema.STRATEGY_ROUGH
                };
                if (double.TryParse(_defaultsStepoverTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var stepover))
                    values.Stepover = stepover;
                break;
            case "DRILL":
                if (double.TryParse(_defaultsDiameterTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var diam))
                    values.Diameter = diam;
                values.Peck = _defaultsPeckCheckBox.Checked ?? false;
                if (double.TryParse(_defaultsPeckDepthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var peckD))
                    values.PeckDepth = peckD;
                break;
        }

        OperationDefaults.SaveDefaults(typeKey, values);
        RhinoApp.WriteLine($"[CamPanel] ✅ Standardwerte für {typeKey} gespeichert.");
    }

    /// <summary>
    /// Resets saved defaults by clearing document UserText overrides.
    /// Machine profile built-in defaults will be used again.
    /// </summary>
    private void ResetDefaults()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        // Clear all CNC_Defaults_* entries from document strings
        // We know the exact suffixes used by OperationDefaults
        var types = new[] { "CONTOUR", "POCKET", "DRILL", "GROOVE" };
        var suffixes = new[] { "Depth", "Feedrate", "Strategy", "Tool", "Stepover", "Width", "Diameter", "PeckDepth", "Peck", "RampEntry" };

        int removed = 0;
        foreach (var type in types)
        {
            foreach (var suffix in suffixes)
            {
                var key = $"CNC_Defaults_{type}_{suffix}";
                if (!string.IsNullOrEmpty(doc.Strings.GetValue(key)))
                {
                    doc.Strings.Delete(key);
                    removed++;
                }
            }
        }

        UpdateDefaultsDisplay();
        RhinoApp.WriteLine($"[CamPanel] Standardwerte auf Maschinenprofil-Defaults zurückgesetzt ({removed} Einträge entfernt).");
    }

    #endregion

    #region Helpers

    private static Label CreateLabel(string text, float size, bool bold)
    {
        return new Label
        {
            Text = text,
            TextColor = FgText,
            Font = bold ? new Font(SystemFont.Bold, size) : new Font(SystemFont.Default, size)
        };
    }

    private static Button CreateToolbarButton(string text, Color accentColor, string? tooltip = null)
    {
        return new Button
        {
            Text = text,
            Height = 26,
            ToolTip = tooltip
        };
    }

    private static Expander CreateSection(string title, Control content, string? subtitle, bool expanded)
    {
        return new Expander
        {
            Expanded = expanded,
            Header = CreateSectionHeader(title, subtitle),
            Content = new StackLayout
            {
                Padding = new Padding(8, 6, 8, 2),
                Spacing = 0,
                Items = { content }
            }
        };
    }

    private static Control CreateSectionHeader(string title, string? subtitle)
    {
        var header = new StackLayout
        {
            Spacing = 1,
            Padding = new Padding(2, 2, 0, 0)
        };

        header.Items.Add(CreateLabel(title, 10, true));
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            header.Items.Add(CreateLabel(subtitle, 8, false));
        }

        return header;
    }

    private static void RunCommand(string commandName)
    {
        RhinoApp.RunScript(commandName, false);
    }

    private static string GetOperationIcon(string type) => type.ToUpperInvariant() switch
    {
        "CONTOUR" => "🔴",
        "POCKET" => "🔵",
        "DRILL" => "🟡",
        "GROOVE" => "🟢",
        _ => "⚪"
    };

    private void UpdateStatusBar(int operations, int tools, int warnings)
    {
        var parts = new List<string>
        {
            $"{operations} Operation{(operations != 1 ? "en" : "")}"
        };

        if (tools > 0)
            parts.Add($"{tools} Werkzeug{(tools != 1 ? "e" : "")}");

        if (warnings > 0)
            parts.Add($"⚠ {warnings} Warnung{(warnings != 1 ? "en" : "")}");

        parts.Add(GetCurrentMachineProfile().MachineKey.ToUpperInvariant());

        _statusLabel.Text = string.Join(" | ", parts);
    }

    private void SetRowVisibility(TableRow row, bool visible)
    {
        // Eto.Forms TableRow doesn't have a Visible property.
        // We work around this by enabling/disabling the cells.
        foreach (var cell in row.Cells)
        {
            if (cell.Control != null)
                cell.Control.Visible = visible;
        }
    }

    private void SetStrategyDropDown(string? strategy)
    {
        _propStrategyDropDown.SelectedIndex = strategy?.ToUpperInvariant() switch
        {
            "FINISH" => 1,
            "BOTH" => 2,
            _ => 0 // Rough
        };
    }

    private string GetSelectedStrategy()
    {
        return _propStrategyDropDown.SelectedIndex switch
        {
            1 => CncOperationSchema.STRATEGY_FINISH,
            2 => CncOperationSchema.STRATEGY_BOTH,
            _ => CncOperationSchema.STRATEGY_ROUGH
        };
    }

    private void SetRampEntryDropDown(string? rampEntry)
    {
        _propRampEntryDropDown.SelectedIndex = rampEntry?.ToUpperInvariant() switch
        {
            "SPIRAL" => 1,
            "PROFILE" => 2,
            _ => 0 // Straight
        };
    }

    private string GetSelectedRampEntry()
    {
        return _propRampEntryDropDown.SelectedIndex switch
        {
            1 => CncOperationSchema.RAMP_SPIRAL,
            2 => CncOperationSchema.RAMP_PROFILE,
            _ => CncOperationSchema.RAMP_STRAIGHT
        };
    }

    #endregion

    #region Cleanup

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnhookObjectEvents();
            RhinoDoc.ActiveDocumentChanged -= OnActiveDocumentChanged;
            RhinoDoc.CloseDocument -= OnDocumentClosed;
            _eventsHooked = false;
        }

        base.Dispose(disposing);
    }

    #endregion

    /// <summary>
    /// Holds state for a single operation entry in the tree.
    /// </summary>
    private sealed record OperationEntry(Guid ObjectId, MachiningOperation Operation, string ObjectName);
}
