using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.UI;

/// <summary>
/// Dockable CAM panel showing all CNC operations in the document.
/// Inspired by RhinoCAM / Fusion 360 CAM panels.
/// Provides operations tree, inline editing, toolpath generation.
/// </summary>
[Guid("c7e3a1d5-4f82-4e9b-b3c6-8d2f1a5e7b09")]
public sealed class CamPanel : Panel
{
    public static readonly Guid PanelId = typeof(CamPanel).GUID;
    public const string PanelDisplayName = "CNC Operations";

    // --- Colors (match Rhino dark theme / ExportPanel style) ---
    private static readonly Color BgDark = Color.FromArgb(45, 45, 48);
    private static readonly Color FgText = Color.FromArgb(220, 220, 220);
    private static readonly Color FgDim = Color.FromArgb(150, 150, 150);
    private static readonly Color AccentContour = Color.FromArgb(220, 60, 60);
    private static readonly Color AccentPocket = Color.FromArgb(60, 120, 220);
    private static readonly Color AccentDrill = Color.FromArgb(220, 200, 40);
    private static readonly Color AccentGroove = Color.FromArgb(60, 180, 80);

    // --- UI Controls ---
    private readonly TreeGridView _operationsTree;
    private TreeGridItemCollection _treeItems = new();

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

        // --- Quick-Add Toolbar ---
        var addContourBtn = CreateToolbarButton("+ Contour", AccentContour);
        addContourBtn.Click += (_, _) => RunCommand("CNCAddContour");
        var addPocketBtn = CreateToolbarButton("+ Pocket", AccentPocket);
        addPocketBtn.Click += (_, _) => RunCommand("CNCAddPocket");
        var addDrillBtn = CreateToolbarButton("+ Drill", AccentDrill);
        addDrillBtn.Click += (_, _) => RunCommand("CNCAddDrill");
        var addGrooveBtn = CreateToolbarButton("+ Groove", AccentGroove);
        addGrooveBtn.Click += (_, _) => RunCommand("CNCAddGroove");

        var toolbarRow1 = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { addContourBtn, addPocketBtn }
        };
        var toolbarRow2 = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { addDrillBtn, addGrooveBtn }
        };
        var toolbar = new StackLayout
        {
            Spacing = 2,
            Items = { toolbarRow1, toolbarRow2 }
        };

        // --- Operations TreeView ---
        _operationsTree = CreateOperationsTree();

        var opsSection = CreateSection("Operations", _operationsTree, null, true);

        // --- Properties Panel ---
        _propTypeLabel = CreateLabel("—", 10, true);
        _propObjectLabel = CreateLabel("—", 9, false);
        _propObjectLabel.TextColor = FgDim;

        _propToolDropDown = new DropDown();
        _propDepthTextBox = new TextBox { PlaceholderText = "mm", Width = 80 };

        _propStrategyDropDown = new DropDown();
        _propStrategyDropDown.Items.Add("Rough");
        _propStrategyDropDown.Items.Add("Finish");
        _propStrategyDropDown.Items.Add("Both");
        _propStrategyDropDown.SelectedIndex = 0;

        _propWidthTextBox = new TextBox { PlaceholderText = "mm", Width = 80 };
        _propDiameterTextBox = new TextBox { PlaceholderText = "mm", Width = 80 };
        _propStepoverTextBox = new TextBox { PlaceholderText = "%", Width = 80 };

        _propPeckCheckBox = new CheckBox { Text = "Peck drilling", TextColor = FgText };
        _propPeckDepthTextBox = new TextBox { PlaceholderText = "mm", Width = 80 };

        _propRampEntryDropDown = new DropDown();
        _propRampEntryDropDown.Items.Add("Straight");
        _propRampEntryDropDown.Items.Add("Spiral");
        _propRampEntryDropDown.Items.Add("Profile");
        _propRampEntryDropDown.SelectedIndex = 0;

        _applyButton = new Button { Text = "✓ Apply", Height = 28 };
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

        var propsSection = CreateSection("Properties", _propertiesPanel, "Selected operation", true);

        // --- Action Buttons ---
        var generateAllBtn = new Button { Text = "▶ Generate All", Height = 30 };
        generateAllBtn.Click += (_, _) => GenerateAllToolpaths();
        var clearAllBtn = new Button { Text = "✕ Clear All", Height = 30 };
        clearAllBtn.Click += (_, _) => ClearAllToolpaths();
        var refreshBtn = new Button { Text = "↻ Refresh", Height = 26 };
        refreshBtn.Click += (_, _) => RefreshOperationsTree();

        var actionButtons = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { generateAllBtn, clearAllBtn, refreshBtn }
        };

        // --- Status Bar ---
        _statusLabel = CreateLabel("No operations", 9, false);
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
                    toolbar,
                    opsSection,
                    propsSection,
                    actionButtons,
                    _statusLabel
                }
            }
        };

        // Initial state
        ShowPropertiesForType(null);
        LoadToolLibrary();
        RefreshOperationsTree();
        HookDocumentEvents();
    }

    #region Operations Tree

    private TreeGridView CreateOperationsTree()
    {
        var tree = new TreeGridView
        {
            Height = 220,
            AllowMultipleSelection = false
        };

        tree.Columns.Add(new GridColumn
        {
            HeaderText = "Operation",
            Width = 200,
            DataCell = new TextBoxCell(0)
        });
        tree.Columns.Add(new GridColumn
        {
            HeaderText = "Tool",
            Width = 100,
            DataCell = new TextBoxCell(1)
        });
        tree.Columns.Add(new GridColumn
        {
            HeaderText = "Depth",
            Width = 60,
            DataCell = new TextBoxCell(2)
        });

        tree.DataStore = new TreeGridItemCollection();

        tree.SelectionChanged += OnTreeSelectionChanged;
        tree.CellDoubleClick += OnTreeCellDoubleClick;

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
            return;
        }

        var operations = CncOperationService.GetAllOperationsInDocument(doc).ToList();
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
                if (toolName != "—") toolNames.Add(toolName);
                else warnings++;

                var depthStr = op.Depth.HasValue
                    ? $"{op.Depth.Value:F1}mm"
                    : "—";

                var childItem = new TreeGridItem
                {
                    Values = new object[] { $"  {objName}", toolName, depthStr },
                    Tag = new OperationEntry(obj.Id, op, objName)
                };

                groupItem.Children.Add(childItem);
            }

            groupItem.Expanded = true;
            _treeItems.Add(groupItem);
        }

        _operationsTree.DataStore = _treeItems;
        UpdateStatusBar(totalOps, toolNames.Count, warnings);
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
            // Zoom to object on double-click
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                var obj = doc.Objects.FindId(entry.ObjectId);
                if (obj != null)
                {
                    doc.Objects.UnselectAll();
                    doc.Objects.Select(entry.ObjectId, true);
                    doc.Views.ActiveView?.ActiveViewport.ZoomBoundingBox(obj.Geometry.GetBoundingBox(true));
                    doc.Views.Redraw();
                }
            }
        }
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
            _propTypeLabel.Text = "No selection";
            _propObjectLabel.Text = "Select an operation above";
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
            RhinoApp.WriteLine("[CamPanel] Object not found — may have been deleted.");
            return;
        }

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

        doc.Views.Redraw();
        RefreshOperationsTree();

        RhinoApp.WriteLine($"[CamPanel] Updated {op.Type} operation on {_selectedOperation.ObjectName}");
    }

    #endregion

    #region Toolpath Generation

    private void GenerateAllToolpaths()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        var operations = CncOperationService.GetAllOperationsInDocument(doc).ToList();
        int generated = 0;

        foreach (var obj in operations)
        {
            var op = CncOperationService.GetOperation(obj);
            if (op == null) continue;

            var toolDiam = op.Diameter ?? GetToolDiameterByName(op.Tool);
            if (toolDiam <= 0) continue;

            // Remove existing toolpath first
            ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);

            // Regenerate
            RegenerateToolpath(doc, obj, op.Type, toolDiam);
            generated++;
        }

        doc.Views.Redraw();
        RhinoApp.WriteLine($"[CamPanel] Generated toolpaths for {generated} operation(s).");
        RefreshOperationsTree();
    }

    private void ClearAllToolpaths()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        var operations = CncOperationService.GetAllOperationsInDocument(doc).ToList();
        int cleared = 0;

        foreach (var obj in operations)
        {
            ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);
            cleared++;
        }

        doc.Views.Redraw();
        RhinoApp.WriteLine($"[CamPanel] Cleared toolpaths for {cleared} operation(s).");
    }

    private void RegenerateToolpath(RhinoDoc doc, RhinoObject obj, string operationType, double toolDiameter)
    {
        // Remove existing toolpath first
        ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);

        if (toolDiameter <= 0) return;

        var geometry = obj.Geometry;
        List<Rhino.Geometry.GeometryBase> toolpathGeometry;

        switch (operationType.ToUpperInvariant())
        {
            case "CONTOUR":
            case "GROOVE":
                if (geometry is Rhino.Geometry.Curve curve)
                {
                    toolpathGeometry = ToolpathVisualizer.CreateContourToolpath(curve, toolDiameter);
                    ToolpathVisualizer.AddToolpathToDocument(doc, obj, operationType, toolpathGeometry);
                }
                break;
            case "POCKET":
                if (geometry is Rhino.Geometry.Curve pocketCurve)
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
                else if (geometry is Rhino.Geometry.Curve drillCurve)
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
            var profile = new ScmProfile(); // Default profile
            var library = _toolLibraryStore.LoadOrCreate(profile);
            _allTools = library.Tools.ToList();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CamPanel] Failed to load tool library: {ex.Message}");
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
            _propToolDropDown.Items.Add(new ListItem
            {
                Text = $"Ø{tool.NominalDiameter:F1} {tool.Name}",
                Key = tool.Id
            });
        }

        if (_propToolDropDown.Items.Count > 0)
            _propToolDropDown.SelectedIndex = 0;
    }

    private void SelectToolInDropDown(string? toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return;

        for (int i = 0; i < _propToolDropDown.Items.Count; i++)
        {
            if (_propToolDropDown.Items[i].Text.Contains(toolName, StringComparison.OrdinalIgnoreCase))
            {
                _propToolDropDown.SelectedIndex = i;
                return;
            }
        }
    }

    private ToolDefinition? GetSelectedTool()
    {
        if (_propToolDropDown.SelectedIndex < 0) return null;
        var selectedKey = (_propToolDropDown.Items[_propToolDropDown.SelectedIndex] as ListItem)?.Key;
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

    #region Context Menu

    private ContextMenu CreateOperationContextMenu(OperationEntry entry)
    {
        var menu = new ContextMenu();

        var editItem = new ButtonMenuItem { Text = "Edit Parameters" };
        editItem.Click += (_, _) =>
        {
            _selectedOperation = entry;
            ShowPropertiesForOperation(entry);
        };
        menu.Items.Add(editItem);

        var removeItem = new ButtonMenuItem { Text = "Remove Operation" };
        removeItem.Click += (_, _) => RemoveOperation(entry);
        menu.Items.Add(removeItem);

        var regenItem = new ButtonMenuItem { Text = "Regenerate Toolpath" };
        regenItem.Click += (_, _) =>
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var obj = doc.Objects.FindId(entry.ObjectId);
            if (obj == null) return;
            var toolDiam = entry.Operation.Diameter ?? GetToolDiameterByName(entry.Operation.Tool);
            RegenerateToolpath(doc, obj, entry.Operation.Type, toolDiam);
            doc.Views.Redraw();
        };
        menu.Items.Add(regenItem);

        return menu;
    }

    private void RemoveOperation(OperationEntry entry)
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        var obj = doc.Objects.FindId(entry.ObjectId);
        if (obj == null) return;

        // Remove toolpath geometry
        ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);
        // Remove operation UserText
        CncOperationService.RemoveOperation(obj);
        // Restore default color
        CncOperationService.RestoreDefaultColor(obj);

        doc.Views.Redraw();
        RefreshOperationsTree();

        RhinoApp.WriteLine($"[CamPanel] Removed {entry.Operation.Type} from {entry.ObjectName}");
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

    private static Button CreateToolbarButton(string text, Color accentColor)
    {
        return new Button
        {
            Text = text,
            Height = 26,
            Width = 100
            // Note: Eto.Forms Button doesn't easily support accent colors on all platforms,
            // but the text makes the function clear
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
            $"{operations} operation{(operations != 1 ? "s" : "")}"
        };

        if (tools > 0)
            parts.Add($"{tools} tool{(tools != 1 ? "s" : "")}");

        if (warnings > 0)
            parts.Add($"⚠ {warnings} warning{(warnings != 1 ? "s" : "")}");

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
