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
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Pipeline;
using CorePlatePreview = RhinoCNCExporter.Core.Models.PlatePreview;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.UI;

/// <summary>
/// Dockable export panel for RhinoCNCExporter.
/// Sprint 4: supports automatic mode detection, multi-plate preview and batch export.
/// </summary>
[Guid("a1f4d2e3-9c87-4b5f-a6d1-3e8f7c2b4a90")]
public sealed class ExportPanel : Panel
{
    public static readonly Guid PanelId = typeof(ExportPanel).GUID;
    public const string PanelDisplayName = "RhinoCNC Export";

    private readonly DropDown _machineDropDown;
    private readonly DropDown _modeDropDown;
    private readonly Label _summaryLabel;
    private readonly Label _recommendationLabel;
    private readonly Label _capabilityLabel;
    private readonly Label _workflowSummaryLabel;
    private readonly Label _workflowSelectionLabel;
    private readonly Label _workflowFocusLabel;
    private readonly ListBox _operationsListBox;
    private readonly TreeGridView _plateTreeView;
    private readonly Label _toolLibraryLabel;
    private readonly Label _strategySummaryLabel;
    private readonly Button _assignDrillsButton;
    private readonly Button _assignInsideContoursButton;
    private readonly Button _assignOutsideContourButton;
    private readonly Button _workflowFocusButton;
    private readonly TextBox _exportPathTextBox;
    private readonly TextBox _stepdownTextBox;
    private readonly TextBox _toleranceTextBox;
    private readonly TextBox _toolDiaTextBox;
    private readonly TextBox _roughAllowanceTextBox;
    private readonly TextBox _zugabeXTextBox;
    private readonly TextBox _zugabeYTextBox;
    private readonly CheckBox _layerStepdownCheckBox;
    private readonly CheckBox _onlySelectionCheckBox;
    private readonly CheckBox _blockDetectionCheckBox;
    private readonly CheckBox _roughFinishPreviewCheckBox;
    private readonly TextArea _reportArea;
    private readonly TextArea _logArea;

    private TreeGridItemCollection _plateTreeItems = new();
    private DocumentExportAnalysis? _latestAnalysis;
    private Dictionary<string, PlateWorkflowSnapshot> _latestWorkflowSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, MachiningToolOverride> _strategyOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly ToolLibraryStore _toolLibraryStore = new();
    private readonly ToolpathPreviewService _toolpathPreviewService = new();

    private static readonly Color BgDark = Color.FromArgb(45, 45, 48);
    private static readonly Color FgText = Color.FromArgb(220, 220, 220);
    private const int SidebarWidth = 340;
    private const string AssignDrillsButtonLabel = "Bohrungen zuordnen";
    private const string AssignInsideContoursButtonLabel = "Innenkonturen zuordnen";
    private const string AssignOutsideContourButtonLabel = "Außenkontur zuordnen";
    private const string WorkflowFocusButtonLabel = "Nächsten offenen Punkt öffnen";

    public ExportPanel()
    {
        BackgroundColor = BgDark;

        var headerLabel = CreateLabel("RhinoCNC Export", 14, true);

        _machineDropDown = new DropDown();
        _machineDropDown.Items.Add("SCM Maestro (.xcs)");
        _machineDropDown.Items.Add("Biesse (.cix)");
        _machineDropDown.Items.Add("Homag (.mpr) — geplant");
        _machineDropDown.SelectedIndex = 0;
        _machineDropDown.SelectedIndexChanged += (_, _) => OnMachineChanged();

        _modeDropDown = new DropDown();
        _modeDropDown.Items.Add("Automatisch");
        _modeDropDown.Items.Add("2D Legacy");
        _modeDropDown.Items.Add("3D Multi-Platte");
        _modeDropDown.SelectedIndex = 0;
        _modeDropDown.SelectedIndexChanged += (_, _) => OnModeChanged();

        var scan3dButton = new Button { Text = "↻ Dokument scannen", Height = 28 };
        scan3dButton.Click += (_, _) => RefreshDocumentAnalysis();

        var modeLayout = new TableLayout
        {
            Spacing = new Size(8, 4),
            Rows =
            {
                new TableRow(CreateLabel("Maschine:", 9, false), new TableCell(_machineDropDown, true)),
                new TableRow(CreateLabel("Export-Modus:", 9, false), new TableCell(_modeDropDown, true)),
                new TableRow(new TableCell(scan3dButton) { ScaleWidth = true })
            }
        };
        var modeSection = CreateSection(
            "Modus",
            modeLayout,
            subtitle: "Maschine, Modus und Dokument-Scan",
            expanded: true);

        _summaryLabel = CreateLabel("Dokument: —", 10, false);
        _recommendationLabel = CreateLabel("Empfehlung: —", 10, false);
        _capabilityLabel = CreateLabel("Capabilities: —", 9, false);
        _workflowSummaryLabel = CreateLabel("Workflow: —", 9, false);
        _workflowSummaryLabel.ID = UiAutomationIds.ExportPanelWorkflowSummary;
        var summarySection = CreateSection(
            "Dokumentanalyse",
            new StackLayout
            {
                Spacing = 4,
                Items = { _summaryLabel, _recommendationLabel, _capabilityLabel, _workflowSummaryLabel }
            },
            subtitle: "Auto-Erkennung und Exportempfehlung",
            expanded: true);

        _operationsListBox = new ListBox { Height = 150 };
        var refreshOpsButton = new Button { Text = "↻ Layer neu laden", Height = 26 };
        refreshOpsButton.Click += (_, _) => RefreshOperations();
        var operationsSection = CreateSection(
            "Legacy-Layer",
            new StackLayout
            {
                Spacing = 4,
                Items = { _operationsListBox, refreshOpsButton }
            },
            subtitle: "2D-Operationen aus Layernamen",
            expanded: false);

        _plateTreeView = CreatePlateTreeView();
        _plateTreeView.SelectionChanged += (_, _) => UpdateWorkflowAssignmentControls();
        var scanPlatesButton = new Button { Text = "↻ Analyse aktualisieren", Height = 26 };
        scanPlatesButton.Click += (_, _) => RefreshDocumentAnalysis();
        var selectAllButton = new Button { Text = "Alle", Height = 26, Width = 60 };
        selectAllButton.Click += (_, _) => SetAllPlateSelections(true);
        var selectNoneButton = new Button { Text = "Keine", Height = 26, Width = 60 };
        selectNoneButton.Click += (_, _) => SetAllPlateSelections(false);

        var plateActions = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { scanPlatesButton, selectAllButton, selectNoneButton }
        };

        _workflowSelectionLabel = CreateLabel("Workflow-Zuordnung: Platte im Baum wählen", 9, false);
        _workflowFocusLabel = CreateLabel("Workflow-Fokus: —", 9, false);
        _workflowFocusLabel.ID = UiAutomationIds.ExportPanelWorkflowFocus;
        _workflowFocusButton = new Button
        {
            Text = WorkflowFocusButtonLabel,
            Height = 28,
            ID = UiAutomationIds.ExportPanelWorkflowFocusAction,
            Enabled = false
        };
        _workflowFocusButton.Click += (_, _) => OpenRecommendedWorkflowAssignment();
        _assignDrillsButton = new Button
        {
            Text = AssignDrillsButtonLabel,
            Height = 28,
            ID = UiAutomationIds.ExportPanelAssignDrills,
            Enabled = false
        };
        _assignDrillsButton.Click += (_, _) => OpenFeatureAssignmentDialog(WorkflowFeatureGroupKind.Drill);
        _assignInsideContoursButton = new Button
        {
            Text = AssignInsideContoursButtonLabel,
            Height = 28,
            ID = UiAutomationIds.ExportPanelAssignInsideContours,
            Enabled = false
        };
        _assignInsideContoursButton.Click += (_, _) => OpenFeatureAssignmentDialog(WorkflowFeatureGroupKind.InsideContour);
        _assignOutsideContourButton = new Button
        {
            Text = AssignOutsideContourButtonLabel,
            Height = 28,
            ID = UiAutomationIds.ExportPanelAssignOutsideContour,
            Enabled = false
        };
        _assignOutsideContourButton.Click += (_, _) => OpenFeatureAssignmentDialog(WorkflowFeatureGroupKind.OutsideContour);

        var assignmentButtons = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { _assignDrillsButton, _assignInsideContoursButton, _assignOutsideContourButton }
        };

        var plateSection = CreateSection("Platten & Workflow",
            new StackLayout
            {
                Spacing = 4,
                Items = { _plateTreeView, plateActions, _workflowSelectionLabel, _workflowFocusLabel, _workflowFocusButton, assignmentButtons }
            },
            subtitle: "3D-Platten, Workflow-Features und Exportauswahl",
            expanded: true);

        _stepdownTextBox = new TextBox { PlaceholderText = "Stepdown (mm)", Text = "3.0" };
        _toleranceTextBox = new TextBox { PlaceholderText = "Toleranz (mm)", Text = "0.05" };
        _toolDiaTextBox = new TextBox { PlaceholderText = "Tool Ø (mm)", Text = "9.5" };
        _roughAllowanceTextBox = new TextBox { PlaceholderText = "Aufmass (mm)", Text = "0.3" };
        _zugabeXTextBox = new TextBox { PlaceholderText = "Zugabe X (mm)", Text = "2.5" };
        _zugabeYTextBox = new TextBox { PlaceholderText = "Zugabe Y (mm)", Text = "2.5" };
        _layerStepdownCheckBox = new CheckBox { Text = "Layer-Stepdown (_Sxx)", Checked = false, TextColor = FgText };
        _onlySelectionCheckBox = new CheckBox { Text = "Nur selektierte Geometrie", Checked = false, TextColor = FgText };
        _blockDetectionCheckBox = new CheckBox { Text = "Block-Detection aktivieren", Checked = true, TextColor = FgText };
        _roughFinishPreviewCheckBox = new CheckBox { Text = "Schrupp/Schlicht Vorschau", Checked = true, TextColor = FgText };

        var settingsLayout = new TableLayout
        {
            Spacing = new Size(8, 4),
            Rows =
            {
                new TableRow(CreateLabel("Stepdown (mm):", 9, false), new TableCell(_stepdownTextBox, true)),
                new TableRow(CreateLabel("Toleranz (mm):", 9, false), new TableCell(_toleranceTextBox, true)),
                new TableRow(CreateLabel("Tool Ø (mm):", 9, false), new TableCell(_toolDiaTextBox, true)),
                new TableRow(CreateLabel("Aufmass (mm):", 9, false), new TableCell(_roughAllowanceTextBox, true)),
                new TableRow(CreateLabel("Zugabe X (mm):", 9, false), new TableCell(_zugabeXTextBox, true)),
                new TableRow(CreateLabel("Zugabe Y (mm):", 9, false), new TableCell(_zugabeYTextBox, true)),
                new TableRow(new TableCell(_layerStepdownCheckBox) { ScaleWidth = true }),
                new TableRow(new TableCell(_onlySelectionCheckBox) { ScaleWidth = true }),
                new TableRow(new TableCell(_blockDetectionCheckBox) { ScaleWidth = true }),
                new TableRow(new TableCell(_roughFinishPreviewCheckBox) { ScaleWidth = true })
            }
        };
        var settingsSection = CreateSection(
            "Einstellungen",
            settingsLayout,
            subtitle: "Toleranzen, Vorschau und Exportoptionen",
            expanded: true);

        _toolLibraryLabel = CreateLabel("Werkzeugdatenbank: —", 9, false);
        _strategySummaryLabel = CreateLabel("Strategien: Auto-Heuristik", 9, false);
        var manageToolsButton = new Button { Text = "Werkzeugmanager", Height = 30, ID = UiAutomationIds.ExportPanelToolManager };
        manageToolsButton.Click += (_, _) => OpenToolLibraryManager();
        var strategyButton = new Button { Text = "Werkzeugzuordnung", Height = 30, ID = UiAutomationIds.ExportPanelToolStrategy };
        strategyButton.Click += (_, _) => OpenToolStrategyDialog();
        var importToolsButton = new Button { Text = "Importieren", Height = 26 };
        importToolsButton.Click += (_, _) => ImportToolLibrary();
        var exportToolsButton = new Button { Text = "Exportieren", Height = 26 };
        exportToolsButton.Click += (_, _) => ExportToolLibrary();
        var resetToolsButton = new Button { Text = "Defaults", Height = 26 };
        resetToolsButton.Click += (_, _) => ResetToolLibrary();
        var previewButton = new Button { Text = "Vorschau erzeugen", Height = 30, ID = UiAutomationIds.ExportPanelGeneratePreview };
        previewButton.Click += (_, _) => GeneratePreview();
        var clearPreviewButton = new Button { Text = "Vorschau löschen", Height = 30, ID = UiAutomationIds.ExportPanelClearPreview };
        clearPreviewButton.Click += (_, _) => ClearPreview();

        var toolButtons = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { importToolsButton, exportToolsButton, resetToolsButton }
        };

        var previewButtons = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { previewButton, clearPreviewButton }
        };

        _exportPathTextBox = new TextBox { PlaceholderText = "Export-Ziel...", ReadOnly = true };
        var browseButton = new Button { Text = "...", Width = 36, Height = 26 };
        browseButton.Click += (_, _) => BrowseExportTarget();

        var pathRow = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { new StackLayoutItem(_exportPathTextBox, true), browseButton }
        };

        var exportButton = new Button { Text = "▶ Export starten", Height = 36, ID = UiAutomationIds.ExportPanelRunExport };
        exportButton.Click += (_, _) => RunExport();

        var actionsSection = CreateSection(
            "Aktionen",
            new StackLayout
            {
                Spacing = 8,
                Items =
                {
                    _toolLibraryLabel,
                    _strategySummaryLabel,
                    manageToolsButton,
                    strategyButton,
                    toolButtons,
                    previewButtons,
                    CreateLabel("Export-Ziel", 9, false),
                    pathRow,
                    exportButton
                }
            },
            subtitle: "Werkzeuge, Vorschau und CNC-Export",
            expanded: true);

        _reportArea = new TextArea
        {
            ReadOnly = true,
            Height = 130,
            Font = new Eto.Drawing.Font("Consolas", 9),
            Text = "Noch kein Export.\n"
        };

        _logArea = new TextArea
        {
            ReadOnly = true,
            Height = 160,
            Font = new Eto.Drawing.Font("Consolas", 9),
            Text = "Bereit.\n"
        };

        var statusTabs = new TabControl
        {
            Height = 230,
            Pages =
            {
                new TabPage
                {
                    Text = "Report",
                    Content = _reportArea
                },
                new TabPage
                {
                    Text = "Log",
                    Content = _logArea
                }
            }
        };

        var statusSection = CreateSection(
            "Status",
            statusTabs,
            subtitle: "Export-Report und Laufzeit-Log",
            expanded: true);

        var topRow = new TableLayout
        {
            Spacing = new Size(12, 0),
            Rows =
            {
                new TableRow(
                    new TableCell(modeSection, true),
                    new TableCell(summarySection, true))
            }
        };

        var leftColumn = new StackLayout
        {
            Spacing = 10,
            Items =
            {
                plateSection,
                operationsSection
            }
        };

        var rightColumn = new StackLayout
        {
            Width = SidebarWidth,
            Spacing = 10,
            Items =
            {
                settingsSection,
                actionsSection,
                statusSection
            }
        };

        var bodyRow = new TableLayout
        {
            Spacing = new Size(12, 0),
            Rows =
            {
                new TableRow(
                    new TableCell(leftColumn, true),
                    new TableCell(rightColumn, false))
            }
        };

        Content = new Scrollable
        {
            Border = BorderType.None,
            Content = new StackLayout
            {
                Padding = new Padding(10),
                Spacing = 10,
                Items =
                {
                    headerLabel,
                    topRow,
                    bodyRow
                }
            }
        };

        RefreshOperations();
        RefreshDocumentAnalysis();
        RefreshToolLibrarySummary();
        RefreshStrategySummary();
    }

    private static Label CreateLabel(string text, float size, bool bold)
    {
        return new Label
        {
            Text = text,
            TextColor = FgText,
            Font = bold ? new Eto.Drawing.Font(SystemFont.Bold, size) : new Eto.Drawing.Font(SystemFont.Default, size)
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

    private static TreeGridView CreatePlateTreeView()
    {
        var view = new TreeGridView
        {
            Height = 260,
            ID = UiAutomationIds.ExportPanelPlateTree,
            AllowMultipleSelection = false
        };

        view.Columns.Add(new GridColumn
        {
            HeaderText = "Export",
            Width = 60,
            Editable = true,
            DataCell = new CheckBoxCell(0)
        });
        view.Columns.Add(new GridColumn
        {
            HeaderText = "Element",
            Width = 170,
            DataCell = new TextBoxCell(1)
        });
        view.Columns.Add(new GridColumn
        {
            HeaderText = "Details",
            Width = 150,
            DataCell = new TextBoxCell(2)
        });
        view.Columns.Add(new GridColumn
        {
            HeaderText = "Layer/Typ",
            Width = 150,
            DataCell = new TextBoxCell(3)
        });
        view.Columns.Add(new GridColumn
        {
            HeaderText = "Mach.",
            Width = 70,
            DataCell = new TextBoxCell(4)
        });

        view.DataStore = new TreeGridItemCollection();
        return view;
    }

    private void OnMachineChanged()
    {
        if (GetMachineFormat() == MachineFormat.Homag)
        {
            Log("⚠ Homag (.mpr) ist noch nicht implementiert.");
        }

        _exportPathTextBox.Text = string.Empty;
        RefreshToolLibrarySummary();
        RefreshStrategySummary();
        RefreshWorkflowStatusVisuals();
    }

    private void OnModeChanged()
    {
        _exportPathTextBox.Text = string.Empty;
        if (_latestAnalysis != null)
        {
            var decision = ExportModeResolver.Decide(_latestAnalysis.Capabilities, GetRequestedMode());
            Log($"Modus: {FormatMode(decision.ResolvedMode)}");
        }
    }

    private void RefreshDocumentAnalysis()
    {
        try
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                _latestAnalysis = null;
                _latestWorkflowSnapshots = new Dictionary<string, PlateWorkflowSnapshot>(StringComparer.OrdinalIgnoreCase);
                _summaryLabel.Text = "Dokument: —";
                _recommendationLabel.Text = "Empfehlung: —";
                _capabilityLabel.Text = "Capabilities: —";
                _workflowSummaryLabel.Text = "Workflow: —";
                PopulatePlateTree(Array.Empty<CorePlatePreview>());
                UpdateWorkflowAssignmentControls();
                return;
            }

            _latestAnalysis = ExportService3D.AnalyzeDocument(doc);
            _latestWorkflowSnapshots = new WorkflowSnapshotService()
                .BuildSnapshots(doc, _latestAnalysis.Plates)
                .ToDictionary(
                    snapshot => BuildPlateSelectionKey(snapshot.Plate.Name, snapshot.Plate.LayerPath),
                    snapshot => snapshot,
                    StringComparer.OrdinalIgnoreCase);
            var caps = _latestAnalysis.Capabilities;

            var totalMachinings = _latestAnalysis.Plates.Sum(p => p.MachiningCount);
            _summaryLabel.Text =
                $"Dokument: {_latestAnalysis.Plates.Count} Platte(n), {_latestAnalysis.TotalBlockCount} Block(s), {totalMachinings} Bearbeitung(en)";
            _recommendationLabel.Text =
                $"Empfehlung: {FormatMode(_latestAnalysis.RecommendedMode)}";
            _capabilityLabel.Text =
                $"Legacy={YesNo(caps.HasLegacyPiece)} · Legacy-Layer={YesNo(caps.HasLegacyMachiningLayers)} · 3D={YesNo(caps.Has3DPlates)} · Blocks={YesNo(caps.HasBlocks)}";
            _workflowSummaryLabel.Text = BuildWorkflowSummary(_latestAnalysis.Plates);

            PopulatePlateTree(_latestAnalysis.Plates);
            UpdateWorkflowAssignmentControls();
            Log($"Dokumentanalyse aktualisiert: {_latestAnalysis.RecommendationReason}");
        }
        catch (Exception ex)
        {
            Log($"❌ Analyse-Fehler: {ex.Message}");
        }
    }

    private void RefreshOperations()
    {
        try
        {
            _operationsListBox.Items.Clear();
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
                return;

            int cutCount = 0, pocketCount = 0, drillCount = 0, patternCount = 0,
                horizontalCount = 0, rowCount = 0, grooveCount = 0, rntCount = 0;

            foreach (var layer in doc.Layers)
            {
                var name = layer.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) || layer.IsDeleted)
                    continue;

                if (Core.LayerParser.LayerRegex.TryParseCut(name, out var cutSpec))
                {
                    cutCount++;
                    _operationsListBox.Items.Add($"CUT: {name} (Z={cutSpec!.Depth}, Ø{cutSpec.ToolDiameter})");
                }
                else if (Core.LayerParser.LayerRegex.TryParsePocket(name, out var pocketSpec))
                {
                    pocketCount++;
                    _operationsListBox.Items.Add($"POCKET: {name} (Z={pocketSpec!.Depth}, Ø{pocketSpec.ToolDiameter})");
                }
                else if (Core.LayerParser.LayerRegex.TryParseDrillPattern(name, out var patternSpec))
                {
                    patternCount++;
                    _operationsListBox.Items.Add($"DRILLPAT: {name} (Ø{patternSpec!.Diameter}, {patternSpec.XCount}x{patternSpec.YCount})");
                }
                else if (Core.LayerParser.LayerRegex.TryParseHorizontalDrill(name, out var horizontalSpec))
                {
                    horizontalCount++;
                    _operationsListBox.Items.Add($"HDRILL: {name} (Ø{horizontalSpec!.Diameter}, Z={horizontalSpec.Depth})");
                }
                else if (Core.LayerParser.LayerRegex.TryParseDrill(name, out var drillSpec))
                {
                    drillCount++;
                    _operationsListBox.Items.Add($"DRILL: {name} (Ø{drillSpec!.Diameter}, Z={drillSpec.Depth})");
                }
                else if (Core.LayerParser.LayerRegex.TryParseRow(name, out var rowSpec))
                {
                    rowCount++;
                    _operationsListBox.Items.Add($"ROW: {name} (Ø{rowSpec!.Diameter}, P={rowSpec.Pitch})");
                }
                else if (Core.LayerParser.LayerRegex.TryParseGrooveChannel(name, out var chSpec))
                {
                    grooveCount++;
                    _operationsListBox.Items.Add($"GROOVE: {name} (W={chSpec!.Width}, Z={chSpec.Depth})");
                }
                else if (Core.LayerParser.LayerRegex.TryParseGrooveRnt(name, out var rntSpec))
                {
                    rntCount++;
                    _operationsListBox.Items.Add($"RNT: {name} (W={rntSpec!.Width}, C={rntSpec.Code})");
                }
            }

            int total = cutCount + pocketCount + drillCount + patternCount + horizontalCount + rowCount + grooveCount + rntCount;
            Log($"Legacy-Layer: {total} Operation(en) erkannt.");
        }
        catch (Exception ex)
        {
            Log($"❌ Layer-Scan-Fehler: {ex.Message}");
        }
    }

    private void PopulatePlateTree(IReadOnlyList<CorePlatePreview> previews)
    {
        var previousSelection = (_plateTreeView.SelectedItem as TreeGridItem)?.Tag as WorkflowTreeSelection;
        _plateTreeItems = new TreeGridItemCollection();
        var planningContext = TryCreateWorkflowPlanningContext();

        if (previews.Count == 0)
        {
            _plateTreeItems.Add(new TreeGridItem
            {
                Values = new object?[] { false, "Keine 3D-Platten erkannt", string.Empty, string.Empty, string.Empty }
            });
            _plateTreeView.DataStore = _plateTreeItems;
            return;
        }

        foreach (var preview in previews.OrderBy(p => p.Plate.Name, StringComparer.OrdinalIgnoreCase))
        {
            var plateKey = BuildPlateSelectionKey(preview.Plate.Name, preview.Plate.LayerPath);
            var plateItem = new TreeGridItem
            {
                Values = new object?[]
                {
                    true,
                    preview.Plate.Name,
                    $"{preview.Plate.LengthX:F1} × {preview.Plate.WidthY:F1} × {preview.Plate.Thickness:F1}",
                    preview.Plate.LayerPath ?? preview.Plate.Source.ToString(),
                    preview.MachiningCount.ToString(CultureInfo.InvariantCulture)
                },
                Tag = new WorkflowTreeSelection(plateKey, preview.Plate.Name, WorkflowFeatureGroupKind.None)
            };

            if (!_latestWorkflowSnapshots.TryGetValue(plateKey, out var workflowSnapshot))
            {
                workflowSnapshot = new WorkflowSnapshotService().BuildSnapshot(RhinoDoc.ActiveDoc, preview.Plate, preview.Blocks);
            }

            var featureGroups = BuildWorkflowFeatureGroups(workflowSnapshot, planningContext);
            if (featureGroups.Count == 0)
            {
                plateItem.Children.Add(new TreeGridItem
                {
                    Values = new object?[]
                    {
                        null,
                        "(keine Workflow-Features)",
                        string.Empty,
                        string.Empty,
                        "0"
                    },
                    Tag = new WorkflowTreeSelection(plateKey, preview.Plate.Name, WorkflowFeatureGroupKind.None)
                });
            }
            else
            {
                foreach (var group in featureGroups)
                {
                    var groupItem = new TreeGridItem
                    {
                        Values = new object?[]
                        {
                            null,
                            group.DisplayName,
                            BuildWorkflowGroupDetail(group),
                            group.SourceSummary,
                            group.Items.Count.ToString(CultureInfo.InvariantCulture)
                        },
                        Tag = new WorkflowTreeSelection(plateKey, preview.Plate.Name, group.Kind)
                    };

                    foreach (var item in group.Items)
                    {
                        groupItem.Children.Add(new TreeGridItem
                        {
                            Values = new object?[]
                            {
                                null,
                                item.Name,
                                item.Detail,
                                BuildWorkflowItemSourceSummary(item),
                                item.MachiningTypeLabel
                            },
                            Tag = new WorkflowTreeSelection(plateKey, preview.Plate.Name, group.Kind)
                        });
                    }

                    groupItem.Expanded = true;
                    plateItem.Children.Add(groupItem);
                }
            }

            plateItem.Expanded = true;
            _plateTreeItems.Add(plateItem);
        }

        _plateTreeView.DataStore = _plateTreeItems;

        if (previousSelection != null)
        {
            var restoredSelection = FindTreeItem(_plateTreeItems, previousSelection);
            if (restoredSelection != null)
            {
                _plateTreeView.SelectedItem = restoredSelection;
            }
        }
    }

    private void UpdateWorkflowAssignmentControls()
    {
        if (!TryGetSelectedWorkflowPlate(out _, out var snapshot) || snapshot == null)
        {
            _workflowSelectionLabel.Text = "Workflow-Zuordnung: Platte im Baum wählen";
            SetFeatureAssignmentButtonState(0, 0, false, 0, 0, false, 0, 0, false);
            UpdateWorkflowFocusControls();
            return;
        }

        var featureGroups = BuildWorkflowFeatureGroups(snapshot, TryCreateWorkflowPlanningContext());
        var drillMetrics = GetWorkflowGroupMetrics(featureGroups, WorkflowFeatureGroupKind.Drill);
        var insideContourMetrics = GetWorkflowGroupMetrics(featureGroups, WorkflowFeatureGroupKind.InsideContour);
        var outsideContourMetrics = GetWorkflowGroupMetrics(featureGroups, WorkflowFeatureGroupKind.OutsideContour);
        var selectedGroup = GetSelectedWorkflowGroupKind();
        var selectedGroupLabel = selectedGroup is WorkflowFeatureGroupKind.Drill or WorkflowFeatureGroupKind.InsideContour or WorkflowFeatureGroupKind.OutsideContour
            ? $" · gewählt: {GetFeatureGroupDisplayName(selectedGroup)}"
            : string.Empty;

        _workflowSelectionLabel.Text =
            $"Workflow-Zuordnung: {snapshot.Plate.Name} · Bohrungen {BuildAssignmentCountSummary(drillMetrics.TotalCount, drillMetrics.OpenCount, drillMetrics.HasAssignmentStatus)} · Innenkonturen {BuildAssignmentCountSummary(insideContourMetrics.TotalCount, insideContourMetrics.OpenCount, insideContourMetrics.HasAssignmentStatus)} · Außenkontur {BuildAssignmentCountSummary(outsideContourMetrics.TotalCount, outsideContourMetrics.OpenCount, outsideContourMetrics.HasAssignmentStatus)}{selectedGroupLabel}";
        SetFeatureAssignmentButtonState(
            drillMetrics.TotalCount,
            drillMetrics.OpenCount,
            drillMetrics.HasAssignmentStatus,
            insideContourMetrics.TotalCount,
            insideContourMetrics.OpenCount,
            insideContourMetrics.HasAssignmentStatus,
            outsideContourMetrics.TotalCount,
            outsideContourMetrics.OpenCount,
            outsideContourMetrics.HasAssignmentStatus);
        UpdateWorkflowFocusControls();
    }

    private void UpdateWorkflowFocusControls()
    {
        if (_latestAnalysis == null)
        {
            _workflowFocusLabel.Text = "Workflow-Fokus: —";
            _workflowFocusButton.Text = WorkflowFocusButtonLabel;
            _workflowFocusButton.Enabled = false;
            return;
        }

        if (!TryGetWorkflowFocusRecommendation(out var recommendation, out var hasKnownAssignmentStatus)
            || recommendation == null)
        {
            _workflowFocusLabel.Text = hasKnownAssignmentStatus
                ? "Workflow-Fokus: Alle bekannten Gruppen sind bereit"
                : "Workflow-Fokus: Werkzeugstatus wird nach Maschinenwahl sichtbar";
            _workflowFocusButton.Text = WorkflowFocusButtonLabel;
            _workflowFocusButton.Enabled = false;
            return;
        }

        _workflowFocusLabel.Text = WorkflowFocusRecommendation.FormatLabel(recommendation);
        _workflowFocusButton.Text = WorkflowFocusRecommendation.FormatActionLabel(recommendation);
        _workflowFocusButton.Enabled = true;
    }

    private bool TryGetWorkflowFocusRecommendation(
        out WorkflowFocusCandidate? recommendation,
        out bool hasKnownAssignmentStatus)
    {
        recommendation = null;
        hasKnownAssignmentStatus = false;

        if (_latestAnalysis == null)
        {
            return false;
        }

        var planningContext = TryCreateWorkflowPlanningContext();

        if (TryGetSelectedWorkflowPlate(out var selectedPreview, out var selectedSnapshot)
            && selectedPreview != null
            && selectedSnapshot != null)
        {
            var selectedPlateKey = BuildPlateSelectionKey(selectedPreview.Plate.Name, selectedPreview.Plate.LayerPath);
            var selectedGroups = BuildWorkflowFeatureGroups(selectedSnapshot, planningContext);
            hasKnownAssignmentStatus = selectedGroups.Any(group => group.HasAssignmentStatus);

            recommendation = WorkflowFocusRecommendation.SelectNext(
                BuildWorkflowFocusCandidates(selectedPlateKey, selectedPreview.Plate.Name, selectedGroups));
            if (recommendation != null)
            {
                return true;
            }
        }

        var allCandidates = new List<WorkflowFocusCandidate>();
        foreach (var preview in _latestAnalysis.Plates)
        {
            var plateKey = BuildPlateSelectionKey(preview.Plate.Name, preview.Plate.LayerPath);
            if (!_latestWorkflowSnapshots.TryGetValue(plateKey, out var snapshot))
            {
                snapshot = new WorkflowSnapshotService().BuildSnapshot(RhinoDoc.ActiveDoc, preview.Plate, preview.Blocks);
            }

            var groups = BuildWorkflowFeatureGroups(snapshot, planningContext);
            hasKnownAssignmentStatus |= groups.Any(group => group.HasAssignmentStatus);
            allCandidates.AddRange(BuildWorkflowFocusCandidates(plateKey, preview.Plate.Name, groups));
        }

        recommendation = WorkflowFocusRecommendation.SelectNext(allCandidates);
        return recommendation != null;
    }

    private void SetFeatureAssignmentButtonState(
        int drillTotalCount,
        int drillOpenCount,
        bool drillHasAssignmentStatus,
        int insideContourTotalCount,
        int insideContourOpenCount,
        bool insideContourHasAssignmentStatus,
        int outsideContourTotalCount,
        int outsideContourOpenCount,
        bool outsideContourHasAssignmentStatus)
    {
        _assignDrillsButton.Enabled = drillTotalCount > 0;
        _assignInsideContoursButton.Enabled = insideContourTotalCount > 0;
        _assignOutsideContourButton.Enabled = outsideContourTotalCount > 0;

        _assignDrillsButton.Text = BuildAssignmentButtonLabel(AssignDrillsButtonLabel, drillTotalCount, drillOpenCount, drillHasAssignmentStatus);
        _assignInsideContoursButton.Text = BuildAssignmentButtonLabel(AssignInsideContoursButtonLabel, insideContourTotalCount, insideContourOpenCount, insideContourHasAssignmentStatus);
        _assignOutsideContourButton.Text = BuildAssignmentButtonLabel(AssignOutsideContourButtonLabel, outsideContourTotalCount, outsideContourOpenCount, outsideContourHasAssignmentStatus);
    }

    private static string BuildAssignmentButtonLabel(string baseLabel, int totalCount, int openCount, bool hasAssignmentStatus)
    {
        return totalCount > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{baseLabel} ({BuildAssignmentCountSummary(totalCount, openCount, hasAssignmentStatus)})")
            : baseLabel;
    }

    private static string BuildAssignmentCountSummary(int totalCount, int openCount, bool hasAssignmentStatus)
    {
        return hasAssignmentStatus
            ? WorkflowStatusText.FormatOpenVsTotal(openCount, totalCount)
            : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, totalCount)} gesamt");
    }

    private WorkflowFeatureGroupKind GetSelectedWorkflowGroupKind()
    {
        return _plateTreeView.SelectedItem is TreeGridItem selectedItem
               && selectedItem.Tag is WorkflowTreeSelection selection
            ? selection.Kind
            : WorkflowFeatureGroupKind.None;
    }

    private void SetAllPlateSelections(bool selected)
    {
        foreach (var item in EnumerateRootItems())
        {
            item.Values[0] = selected;
        }

        _plateTreeView.ReloadData();
    }

    private void BrowseExportTarget()
    {
        try
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                Log("⚠ Kein aktives Rhino-Dokument.");
                return;
            }

            EnsureAnalysis();
            var decision = GetCurrentDecision();
            var extension = GetFileExtensionForMachine();

            if (decision.ResolvedMode == ExportMode.MultiPlate3D)
            {
                var folderDialog = new SelectFolderDialog
                {
                    Title = "Export-Ordner wählen"
                };

                if (folderDialog.ShowDialog(this) == DialogResult.Ok
                    && !string.IsNullOrWhiteSpace(folderDialog.Directory))
                {
                    _exportPathTextBox.Text = folderDialog.Directory;
                }
            }
            else
            {
                var defaultName = string.IsNullOrWhiteSpace(doc.Name)
                    ? "program" + extension
                    : Path.ChangeExtension(doc.Name, extension);

                var saveDialog = new SaveFileDialog
                {
                    Title = "Export-Datei wählen",
                    FileName = defaultName
                };
                saveDialog.Filters.Add(new FileFilter($"CNC Datei (*{extension})", extension));

                if (saveDialog.ShowDialog(this) == DialogResult.Ok
                    && !string.IsNullOrWhiteSpace(saveDialog.FileName))
                {
                    _exportPathTextBox.Text = saveDialog.FileName;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"❌ Pfad-Fehler: {ex.Message}");
        }
    }

    private void RunExport()
    {
        try
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                Log("❌ Kein aktives Rhino-Dokument.");
                return;
            }

            EnsureAnalysis();
            var decision = GetCurrentDecision();
            if (!decision.IsExecutable)
            {
                Log($"❌ {decision.Reason}");
                UpdateReport($"Export nicht möglich.\n{decision.Reason}");
                return;
            }

            var path = _exportPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                BrowseExportTarget();
                path = _exportPathTextBox.Text;
                if (string.IsNullOrWhiteSpace(path))
                    return;
            }

            var requestedMode = GetRequestedMode();
            var machineFormat = GetMachineFormat();
            var selectedPlateKeys = new HashSet<string>(GetSelectedPlateKeys(), StringComparer.OrdinalIgnoreCase);

            if (decision.ResolvedMode == ExportMode.MultiPlate3D && selectedPlateKeys.Count == 0)
            {
                Log("❌ Keine Platte in der Baumansicht ausgewählt.");
                UpdateReport("Export nicht möglich.\nKeine Platte ausgewählt.");
                return;
            }

            bool layerStepdown = _layerStepdownCheckBox.Checked ?? false;
            bool onlySelection = _onlySelectionCheckBox.Checked ?? false;
            bool blockDetection = _blockDetectionCheckBox.Checked ?? true;
            if (decision.ResolvedMode == ExportMode.MultiPlate3D && onlySelection)
            {
                Log("ℹ Nur selektierte Geometrie wird im 3D-Modus ignoriert.");
            }

            double zugabeX = ParseDoubleOrDefault(_zugabeXTextBox.Text, 2.5);
            double zugabeY = ParseDoubleOrDefault(_zugabeYTextBox.Text, 2.5);

            Log($"Exportiere {FormatMode(requestedMode)} → {FormatMode(decision.ResolvedMode)} ...");

            var result = ExportService3D.ExportDocument(
                doc,
                path,
                machineFormat,
                requestedMode,
                blockDetection,
                onlySelection,
                layerStepdown,
                zugabeX,
                zugabeY,
                selectedPlateKeys);

            if (!result.Success)
            {
                Log($"❌ Export fehlgeschlagen: {result.Error ?? "Unbekannter Fehler"}");
                UpdateReport($"Export fehlgeschlagen.\n{result.Error}");
                return;
            }

            foreach (var file in result.ExportedFiles)
            {
                RhinoApp.WriteLine($"[RhinoCNCExporter] CNC erstellt: {file}");
            }

            Log($"✅ Export erfolgreich: {result.ExportedFiles.Count} Datei(en)");
            UpdateReport(BuildReportText(result));
        }
        catch (Exception ex)
        {
            Log($"❌ Export-Fehler: {ex.Message}");
            UpdateReport($"Export-Fehler.\n{ex.Message}");
        }
    }

    private void EnsureAnalysis()
    {
        if (_latestAnalysis == null)
        {
            RefreshDocumentAnalysis();
        }
    }

    private void RefreshToolLibrarySummary()
    {
        if (!TryGetCurrentProfile(out var profile, out var error))
        {
            _toolLibraryLabel.Text = $"Werkzeugdatenbank: {error}";
            RefreshStrategySummary();
            return;
        }

        try
        {
            var library = _toolLibraryStore.LoadOrCreate(profile!);
            _toolLibraryLabel.Text =
                $"Werkzeugdatenbank: {library.Tools.Count} Werkzeuge · {library.Holders.Count} Halter ({profile!.MachineKey})";
        }
        catch (Exception ex)
        {
            _toolLibraryLabel.Text = $"Werkzeugdatenbank: Fehler ({ex.Message})";
        }

        RefreshStrategySummary();
        RefreshWorkflowStatusVisuals();
    }

    private void ImportToolLibrary()
    {
        if (!TryGetCurrentProfile(out var profile, out var error))
        {
            Log($"❌ {error}");
            return;
        }

        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Werkzeugdatenbank importieren"
            };
            dialog.Filters.Add(new FileFilter("JSON Datei (*.json)", ".json"));

            if (dialog.ShowDialog(this) != DialogResult.Ok || string.IsNullOrWhiteSpace(dialog.FileName))
                return;

            var library = _toolLibraryStore.Import(profile!, dialog.FileName);
            RefreshToolLibrarySummary();
            Log($"✅ Werkzeugdatenbank importiert: {library.Tools.Count} Werkzeuge");
        }
        catch (Exception ex)
        {
            Log($"❌ Werkzeug-Import fehlgeschlagen: {ex.Message}");
        }
    }

    private void ExportToolLibrary()
    {
        if (!TryGetCurrentProfile(out var profile, out var error))
        {
            Log($"❌ {error}");
            return;
        }

        try
        {
            var library = _toolLibraryStore.LoadOrCreate(profile!);
            var dialog = new SaveFileDialog
            {
                Title = "Werkzeugdatenbank exportieren",
                FileName = $"{profile!.MachineKey}-tools.json"
            };
            dialog.Filters.Add(new FileFilter("JSON Datei (*.json)", ".json"));

            if (dialog.ShowDialog(this) != DialogResult.Ok || string.IsNullOrWhiteSpace(dialog.FileName))
                return;

            _toolLibraryStore.Export(dialog.FileName, library);
            Log($"✅ Werkzeugdatenbank exportiert: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            Log($"❌ Werkzeug-Export fehlgeschlagen: {ex.Message}");
        }
    }

    private void ResetToolLibrary()
    {
        if (!TryGetCurrentProfile(out var profile, out var error))
        {
            Log($"❌ {error}");
            return;
        }

        try
        {
            var library = _toolLibraryStore.ResetToDefaults(profile!);
            RefreshToolLibrarySummary();
            Log($"✅ Default-Werkzeuge geladen: {library.Tools.Count} Werkzeuge");
        }
        catch (Exception ex)
        {
            Log($"❌ Default-Werkzeuge konnten nicht geladen werden: {ex.Message}");
        }
    }

    private void OpenToolLibraryManager()
    {
        if (!TryGetCurrentProfile(out var profile, out var error))
        {
            Log($"❌ {error}");
            return;
        }

        try
        {
            var library = _toolLibraryStore.LoadOrCreate(profile!);
            var dialog = new ToolLibraryManagerDialog(library);
            var result = dialog.ShowModal(this);
            if (result == null)
                return;

            _toolLibraryStore.Save(profile!, result);
            RefreshToolLibrarySummary();
            Log($"✅ Werkzeugmanager gespeichert: {result.Tools.Count} Werkzeuge, {result.Holders.Count} Halter");
        }
        catch (Exception ex)
        {
            Log($"❌ Werkzeugmanager-Fehler: {ex.Message}");
        }
    }

    private void OpenToolStrategyDialog()
    {
        if (!TryGetCurrentProfile(out var profile, out var error))
        {
            Log($"❌ {error}");
            return;
        }

        try
        {
            EnsureAnalysis();
            if (_latestAnalysis == null)
            {
                Log("❌ Keine Dokumentanalyse verfügbar.");
                return;
            }

            var previews = GetSelectedOrAllPreviews(_latestAnalysis);
            if (previews.Count == 0)
            {
                Log("❌ Keine Bearbeitungen für die Werkzeugzuordnung gefunden.");
                return;
            }

            var library = _toolLibraryStore.LoadOrCreate(profile!);
            var baseOptions = CreatePreviewPlanningOptions(includeOverrides: false);
            var items = BuildOperationStrategyItems(library, previews, baseOptions);
            if (items.Count == 0)
            {
                Log("❌ Keine Bearbeitungen für die Werkzeugzuordnung gefunden.");
                return;
            }

            var dialog = new ToolStrategyDialog(
                library,
                items,
                _strategyOverrides.Values.ToArray(),
                baseOptions);

            var result = dialog.ShowModal(this);
            if (result == null)
                return;

            ApplyStrategyOverrides(items, result);
            RefreshStrategySummary();
            Log($"✅ Werkzeugzuordnung gespeichert: {_strategyOverrides.Count} Override(s)");
            GeneratePreview();
        }
        catch (Exception ex)
        {
            Log($"❌ Werkzeugzuordnung fehlgeschlagen: {ex.Message}");
        }
    }

    private void OpenRecommendedWorkflowAssignment()
    {
        try
        {
            EnsureAnalysis();
            if (!TryGetWorkflowFocusRecommendation(out var recommendation, out _)
                || recommendation == null)
            {
                Log("ℹ Keine offenen Workflow-Zuordnungen gefunden.");
                return;
            }

            if (!Enum.TryParse<WorkflowFeatureGroupKind>(recommendation.GroupKey, ignoreCase: true, out var groupKind))
            {
                Log($"⚠ Unbekannte Workflow-Gruppe: {recommendation.GroupKey}");
                return;
            }

            var target = FindTreeItem(
                _plateTreeItems,
                new WorkflowTreeSelection(recommendation.PlateKey, recommendation.PlateName, groupKind));
            if (target == null)
            {
                Log($"⚠ Workflow-Fokus konnte im Baum nicht gefunden werden: {recommendation.PlateName} · {recommendation.GroupDisplayName}");
                return;
            }

            _plateTreeView.SelectedItem = target;
            OpenFeatureAssignmentDialog(groupKind);
        }
        catch (Exception ex)
        {
            Log($"❌ Workflow-Fokus konnte nicht geöffnet werden: {ex.Message}");
        }
    }

    private void OpenFeatureAssignmentDialog(WorkflowFeatureGroupKind groupKind)
    {
        if (!TryGetCurrentProfile(out var profile, out var error))
        {
            Log($"❌ {error}");
            return;
        }

        try
        {
            EnsureAnalysis();
            if (!TryGetSelectedWorkflowPlate(out _, out var snapshot) || snapshot == null)
            {
                Log("⚠ Für die direkte Zuordnung zuerst eine Platte im Workflow-Baum auswählen.");
                return;
            }

            var library = _toolLibraryStore.LoadOrCreate(profile!);
            var baseOptions = CreatePreviewPlanningOptions(includeOverrides: false);
            var items = BuildOperationStrategyItems(library, snapshot, baseOptions)
                .Where(item => MatchesWorkflowFeatureGroup(snapshot, item.Machining, groupKind))
                .ToList();

            if (items.Count == 0)
            {
                Log($"⚠ Keine {GetFeatureGroupDisplayName(groupKind)} für {snapshot.Plate.Name} gefunden.");
                return;
            }

            var dialog = new ToolStrategyDialog(
                library,
                items,
                _strategyOverrides.Values.ToArray(),
                baseOptions,
                focusMissingAssignments: true);

            var result = dialog.ShowModal(this);
            if (result == null)
                return;

            ApplyStrategyOverrides(items, result);
            RefreshStrategySummary();
            Log($"✅ {GetFeatureGroupDisplayName(groupKind)} für {snapshot.Plate.Name} gespeichert ({items.Count} Bearbeitung(en)).");
            GeneratePreview();
        }
        catch (Exception ex)
        {
            Log($"❌ Workflow-Zuordnung fehlgeschlagen: {ex.Message}");
        }
    }

    private void GeneratePreview()
    {
        try
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                Log("❌ Kein aktives Rhino-Dokument.");
                return;
            }

            EnsureAnalysis();
            if (_latestAnalysis == null)
            {
                Log("❌ Keine Dokumentanalyse verfügbar.");
                return;
            }

            var selectedPlateKeys = new HashSet<string>(GetSelectedPlateKeys(), StringComparer.OrdinalIgnoreCase);
            var options = CreatePreviewPlanningOptions(includeOverrides: true);

            var result = _toolpathPreviewService.GeneratePreview(
                doc,
                GetMachineFormat(),
                _latestAnalysis,
                selectedPlateKeys,
                options);

            if (!result.Success)
            {
                Log($"❌ Vorschau fehlgeschlagen: {result.Error ?? "Unbekannter Fehler"}");
                return;
            }

            // Also regenerate toolpaths for UserText-based interactive CAM operations
            // using the unified ToolpathVisualizer (CNC_Toolpaths layer)
            var interactiveOps = CncOperationService.GetAllOperationsInDocument(doc).ToList();
            int interactiveCount = 0;
            foreach (var obj in interactiveOps)
            {
                var op = CncOperationService.GetOperation(obj);
                if (op == null) continue;

                var toolDiam = op.Diameter ?? 0;
                if (toolDiam <= 0 && !string.IsNullOrEmpty(op.Tool))
                {
                    // Try to resolve diameter from current tool library
                    if (TryGetCurrentProfile(out var prof, out _) && prof != null)
                    {
                        var lib = _toolLibraryStore.LoadOrCreate(prof);
                        var toolDef = lib.Tools.FirstOrDefault(t =>
                            t.Name.Equals(op.Tool, StringComparison.OrdinalIgnoreCase));
                        toolDiam = toolDef?.NominalDiameter ?? 0;
                    }
                }

                if (toolDiam <= 0) continue;

                ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);
                RegenerateInteractiveToolpath(doc, obj, op.Type, toolDiam, op);
                interactiveCount++;
            }

            RefreshToolLibrarySummary();
            var interactiveMsg = interactiveCount > 0 ? $", {interactiveCount} interaktive Op." : "";
            Log($"✅ Vorschau erstellt: {result.PlateCount} Platte(n), {result.OperationCount} Pfade, {result.ObjectCount} Objekte{interactiveMsg}");
        }
        catch (Exception ex)
        {
            Log($"❌ Vorschau-Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Regenerates toolpath visualization for an interactive CNC operation
    /// using the unified ToolpathVisualizer system.
    /// </summary>
    private static void RegenerateInteractiveToolpath(RhinoDoc doc, Rhino.DocObjects.RhinoObject obj, string operationType, double toolDiameter, MachiningOperation op)
    {
        var geometry = obj.Geometry;

        switch (operationType.ToUpperInvariant())
        {
            case "CONTOUR":
            case "GROOVE":
                if (geometry is Rhino.Geometry.Curve curve)
                {
                    var tpGeom = ToolpathVisualizer.CreateContourToolpath(curve, toolDiameter);
                    ToolpathVisualizer.AddToolpathToDocument(doc, obj, operationType, tpGeom);
                }
                break;
            case "POCKET":
                if (geometry is Rhino.Geometry.Curve pocketCurve)
                {
                    double stepover = 50.0;
                    if (op.Stepover.HasValue) stepover = op.Stepover.Value;
                    var tpGeom = ToolpathVisualizer.CreatePocketToolpath(pocketCurve, toolDiameter, stepover);
                    ToolpathVisualizer.AddToolpathToDocument(doc, obj, operationType, tpGeom);
                }
                break;
            case "DRILL":
                Rhino.Geometry.Point3d center;
                if (geometry is Rhino.Geometry.Point pt)
                    center = pt.Location;
                else if (geometry is Rhino.Geometry.Curve drillCurve)
                    center = drillCurve.PointAtStart;
                else
                    return;

                var drillGeom = ToolpathVisualizer.CreateDrillToolpath(center, toolDiameter);
                ToolpathVisualizer.AddToolpathToDocument(doc, obj, operationType, drillGeom);
                break;
        }
    }

    private void ClearPreview()
    {
        try
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                Log("❌ Kein aktives Rhino-Dokument.");
                return;
            }

            var removed = _toolpathPreviewService.ClearPreview(doc);
            var interactiveCleared = ClearInteractivePreview(doc);

            if (interactiveCleared > 0)
            {
                Log($"✅ Vorschau gelöscht: {removed} Preview-Objekt(e), {interactiveCleared} interaktive Operation(en) bereinigt");
            }
            else
            {
                Log($"✅ Vorschau gelöscht: {removed} Objekt(e)");
            }
        }
        catch (Exception ex)
        {
            Log($"❌ Vorschau konnte nicht gelöscht werden: {ex.Message}");
        }
    }

    private static int ClearInteractivePreview(RhinoDoc doc)
    {
        var cleared = 0;
        foreach (var obj in CncOperationService.GetAllOperationsInDocument(doc))
        {
            var has2DPreview = !string.IsNullOrWhiteSpace(obj.Attributes.GetUserString(CncOperationSchema.CNC_GROUP_INDEX));
            var has3DPreview = !string.IsNullOrWhiteSpace(obj.Attributes.GetUserString("CNC_GroupIndex3D"));

            ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);
            ToolpathVisualizer.RemoveToolpath3DGeometry(doc, obj);

            if (has2DPreview || has3DPreview)
            {
                cleared++;
            }
        }

        return cleared;
    }

    private bool TryGetCurrentProfile(out IMachineProfile? profile, out string? error)
    {
        error = null;
        profile = GetMachineFormat() switch
        {
            MachineFormat.Xilog => new MaestroCadTProfile(),
            MachineFormat.Biesse => new BiesseProfile(),
            _ => null
        };

        if (profile == null)
        {
            error = "Maschinenprofil ist noch nicht implementiert.";
            return false;
        }

        return true;
    }

    private ExportModeDecision GetCurrentDecision()
    {
        var capabilities = _latestAnalysis?.Capabilities ?? new DocumentCapabilities();
        return ExportModeResolver.Decide(capabilities, GetRequestedMode());
    }

    private ExportMode GetRequestedMode()
    {
        return _modeDropDown.SelectedIndex switch
        {
            1 => ExportMode.LegacyOnly,
            2 => ExportMode.MultiPlate3D,
            _ => ExportMode.Automatic
        };
    }

    private MachineFormat GetMachineFormat()
    {
        return _machineDropDown.SelectedIndex switch
        {
            1 => MachineFormat.Biesse,
            2 => MachineFormat.Homag,
            _ => MachineFormat.Xilog
        };
    }

    private string GetFileExtensionForMachine()
    {
        return GetMachineFormat() switch
        {
            MachineFormat.Biesse => ".cix",
            MachineFormat.Homag => ".mpr",
            _ => ".xcs"
        };
    }

    private IEnumerable<string> GetSelectedPlateKeys()
    {
        foreach (var item in EnumerateRootItems())
        {
            var values = item.Values.Cast<object?>().ToList();
            var isSelected = values.Count > 0 && values[0] is bool selected && selected;
            var name = values.Count > 1 ? values[1] as string : null;
            var layerOrSource = values.Count > 3 ? values[3] as string : null;
            if (isSelected && !string.IsNullOrWhiteSpace(name) && name != "Keine 3D-Platten erkannt")
            {
                yield return BuildPlateSelectionKey(name, layerOrSource);
            }
        }
    }

    private static string BuildPlateSelectionKey(string name, string? layerOrSource)
    {
        if (!string.IsNullOrWhiteSpace(layerOrSource)
            && !Enum.TryParse<PlateSource>(layerOrSource, ignoreCase: true, out _))
        {
            return layerOrSource;
        }

        return name;
    }

    private IEnumerable<TreeGridItem> EnumerateRootItems()
    {
        foreach (var item in _plateTreeItems)
        {
            if (item is TreeGridItem treeItem)
            {
                yield return treeItem;
            }
        }
    }

    private ToolpathPlanningOptions CreatePreviewPlanningOptions(bool includeOverrides)
    {
        return new ToolpathPlanningOptions
        {
            EnableRoughingStrategies = _roughFinishPreviewCheckBox.Checked ?? true,
            DefaultStockToLeave = ParseDoubleOrDefault(_roughAllowanceTextBox.Text, 0.3),
            StrategyOverrides = includeOverrides
                ? _strategyOverrides.Values.ToArray()
                : Array.Empty<MachiningToolOverride>()
        };
    }

    private IReadOnlyList<CorePlatePreview> GetSelectedOrAllPreviews(DocumentExportAnalysis analysis)
    {
        var selectedPlateKeys = new HashSet<string>(GetSelectedPlateKeys(), StringComparer.OrdinalIgnoreCase);
        if (selectedPlateKeys.Count == 0)
            return analysis.Plates;

        return analysis.Plates
            .Where(preview => selectedPlateKeys.Contains(BuildPlateSelectionKey(preview.Plate.Name, preview.Plate.LayerPath)))
            .ToList();
    }

    private IReadOnlyList<OperationStrategyItem> BuildOperationStrategyItems(
        ToolLibrary library,
        IReadOnlyList<CorePlatePreview> previews,
        ToolpathPlanningOptions baseOptions)
    {
        var doc = RhinoDoc.ActiveDoc;
        var workflowSnapshots = new WorkflowSnapshotService().BuildSnapshots(doc, previews);

        return workflowSnapshots
            .SelectMany(snapshot => BuildOperationStrategyItems(library, snapshot, baseOptions))
            .ToList();
    }

    private static IReadOnlyList<OperationStrategyItem> BuildOperationStrategyItems(
        ToolLibrary library,
        PlateWorkflowSnapshot snapshot,
        ToolpathPlanningOptions baseOptions)
    {
        var items = new List<OperationStrategyItem>();
        var plate = snapshot.Plate with
        {
            Machinings = snapshot.CombinedMachinings
        };

        for (var index = 0; index < plate.Machinings.Count; index++)
        {
            var machining = plate.Machinings[index];
            items.Add(new OperationStrategyItem
            {
                OperationKey = ToolpathPlanner.BuildOperationKey(plate, machining, index),
                PlateName = plate.Name,
                OperationName = machining.Name,
                MachiningType = ToolpathPlanner.GetMachiningType(machining),
                Machining = machining,
                AutoStrategy = MachiningStrategy.CreateDefault(machining, library, baseOptions),
                SupportsRoughing = baseOptions.EnableRoughingStrategies
                    && (machining is RoutingMachining or RoutingWithArcsMachining or PocketMachining)
            });
        }

        return items;
    }

    private void ApplyStrategyOverrides(
        IReadOnlyList<OperationStrategyItem> scopedItems,
        IReadOnlyList<MachiningToolOverride> result)
    {
        var next = new Dictionary<string, MachiningToolOverride>(_strategyOverrides, StringComparer.OrdinalIgnoreCase);
        foreach (var operationKey in scopedItems.Select(item => item.OperationKey))
        {
            next.Remove(operationKey);
        }

        foreach (var item in result.Where(static item => item.HasOverride))
        {
            next[item.OperationKey] = item;
        }

        _strategyOverrides = next;
        RefreshWorkflowStatusVisuals();
    }

    private void RefreshWorkflowStatusVisuals()
    {
        if (_latestAnalysis == null)
        {
            UpdateWorkflowAssignmentControls();
            return;
        }

        PopulatePlateTree(_latestAnalysis.Plates);
        UpdateWorkflowAssignmentControls();
    }

    private bool TryGetSelectedWorkflowPlate(
        out CorePlatePreview? preview,
        out PlateWorkflowSnapshot? snapshot)
    {
        preview = null;
        snapshot = null;

        if (_latestAnalysis == null)
            return false;

        if (_plateTreeView.SelectedItem is not TreeGridItem selectedItem
            || selectedItem.Tag is not WorkflowTreeSelection selection)
        {
            return false;
        }

        preview = _latestAnalysis.Plates.FirstOrDefault(item =>
            string.Equals(
                BuildPlateSelectionKey(item.Plate.Name, item.Plate.LayerPath),
                selection.PlateKey,
                StringComparison.OrdinalIgnoreCase));

        if (preview == null)
            return false;

        return _latestWorkflowSnapshots.TryGetValue(selection.PlateKey, out snapshot);
    }

    private IReadOnlyList<WorkflowFeatureGroupView> BuildWorkflowFeatureGroups(
        PlateWorkflowSnapshot snapshot,
        WorkflowPlanningContext? planningContext)
    {
        var plate = snapshot.Plate with
        {
            Machinings = snapshot.CombinedMachinings
        };

        return snapshot.CombinedMachinings
            .Select((machining, index) =>
            {
                var assignmentStatus = EvaluateWorkflowAssignmentStatus(plate, machining, index, planningContext);
                return new WorkflowFeatureItemView(
                    ClassifyWorkflowFeatureGroup(snapshot, machining),
                    machining.Name,
                    BuildWorkflowFeatureDetail(machining),
                    GetWorkflowSourceLabel(machining.Source),
                    assignmentStatus.ItemLabel,
                    ToolpathPlanner.GetMachiningType(machining).ToString(),
                    assignmentStatus);
            })
            .GroupBy(item => item.Kind)
            .OrderBy(group => GetWorkflowGroupSortOrder(group.Key))
            .Select(group =>
            {
                var items = group.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var totalCount = items.Count;
                var knownAssignmentCount = items.Count(item => item.AssignmentStatus.IsKnown);
                var openCount = items.Count(item => item.AssignmentStatus.IsKnown && !item.AssignmentStatus.IsReady);
                var overrideCount = items.Count(item => item.AssignmentStatus.HasOverride);
                var hasAssignmentStatus = knownAssignmentCount > 0;

                return new WorkflowFeatureGroupView(
                    group.Key,
                    GetFeatureGroupDisplayName(group.Key),
                    totalCount,
                    openCount,
                    overrideCount,
                    hasAssignmentStatus,
                    hasAssignmentStatus
                        ? WorkflowStatusText.FormatOpenVsTotal(openCount, totalCount)
                        : string.Create(CultureInfo.InvariantCulture, $"{totalCount} Feature(s)"),
                    BuildWorkflowAssignmentSummary(items.Select(item => item.AssignmentStatus)),
                    BuildWorkflowSourceSummary(items.Select(item => item.SourceLabel)),
                    items);
            })
            .ToList();
    }

    private WorkflowPlanningContext? TryCreateWorkflowPlanningContext()
    {
        if (!TryGetCurrentProfile(out var profile, out _)
            || profile == null)
        {
            return null;
        }

        try
        {
            return new WorkflowPlanningContext(
                _toolLibraryStore.LoadOrCreate(profile),
                CreatePreviewPlanningOptions(includeOverrides: true));
        }
        catch
        {
            return null;
        }
    }

    private static WorkflowAssignmentStatus EvaluateWorkflowAssignmentStatus(
        Plate plate,
        Machining machining,
        int machiningIndex,
        WorkflowPlanningContext? planningContext)
    {
        if (planningContext == null)
        {
            return new WorkflowAssignmentStatus(false, false, false, null);
        }

        var operationKey = ToolpathPlanner.BuildOperationKey(plate, machining, machiningIndex);
        var strategyOverride = planningContext.Options.FindOverride(operationKey);
        var strategy = MachiningStrategy.CreateDefault(
            machining,
            planningContext.ToolLibrary,
            planningContext.Options,
            strategyOverride);

        if (strategy.FinishingTool == null)
        {
            return new WorkflowAssignmentStatus(false, false, true, "ohne Werkzeug");
        }

        return strategyOverride?.HasOverride == true
            ? new WorkflowAssignmentStatus(true, true, true, "Override")
            : new WorkflowAssignmentStatus(true, false, true, "Auto");
    }

    private static string? BuildWorkflowAssignmentSummary(IEnumerable<WorkflowAssignmentStatus> assignmentStates)
    {
        var knownStates = assignmentStates
            .Where(state => state.IsKnown && !string.IsNullOrWhiteSpace(state.ItemLabel))
            .ToList();

        if (knownStates.Count == 0)
        {
            return null;
        }

        var overrideCount = knownStates.Count(state => state.HasOverride);
        return overrideCount > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{overrideCount} Override(s)")
            : null;
    }

    private static string BuildWorkflowGroupDetail(WorkflowFeatureGroupView group)
    {
        return string.IsNullOrWhiteSpace(group.AssignmentSummary)
            ? group.Summary
            : string.Create(CultureInfo.InvariantCulture, $"{group.Summary} · {group.AssignmentSummary}");
    }

    private static string BuildWorkflowItemSourceSummary(WorkflowFeatureItemView item)
    {
        return string.IsNullOrWhiteSpace(item.AssignmentLabel)
            ? item.SourceLabel
            : string.Create(CultureInfo.InvariantCulture, $"{item.SourceLabel} · {item.AssignmentLabel}");
    }

    private static TreeGridItem? FindTreeItem(
        TreeGridItemCollection items,
        WorkflowTreeSelection selection)
    {
        foreach (var item in items.OfType<TreeGridItem>())
        {
            var match = FindTreeItem(item, selection);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static TreeGridItem? FindTreeItem(
        TreeGridItem item,
        WorkflowTreeSelection selection)
    {
        if (item.Tag is WorkflowTreeSelection currentSelection
            && string.Equals(currentSelection.PlateKey, selection.PlateKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(currentSelection.PlateName, selection.PlateName, StringComparison.OrdinalIgnoreCase)
            && currentSelection.Kind == selection.Kind)
        {
            return item;
        }

        foreach (var child in item.Children.OfType<TreeGridItem>())
        {
            var match = FindTreeItem(child, selection);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static (int TotalCount, int OpenCount, bool HasAssignmentStatus) GetWorkflowGroupMetrics(
        IReadOnlyList<WorkflowFeatureGroupView> groups,
        WorkflowFeatureGroupKind kind)
    {
        var group = groups.FirstOrDefault(item => item.Kind == kind);
        return group == null
            ? (0, 0, false)
            : (group.TotalCount, group.OpenCount, group.HasAssignmentStatus);
    }

    private static IReadOnlyList<WorkflowFocusCandidate> BuildWorkflowFocusCandidates(
        string plateKey,
        string plateName,
        IReadOnlyList<WorkflowFeatureGroupView> groups)
    {
        return groups
            .Where(group => group.Kind is WorkflowFeatureGroupKind.Drill or WorkflowFeatureGroupKind.InsideContour or WorkflowFeatureGroupKind.OutsideContour)
            .Select(group => new WorkflowFocusCandidate(
                plateKey,
                plateName,
                group.Kind.ToString(),
                group.DisplayName,
                GetWorkflowGroupSortOrder(group.Kind),
                group.OpenCount,
                group.TotalCount,
                group.HasAssignmentStatus))
            .ToList();
    }

    private static bool MatchesWorkflowFeatureGroup(
        PlateWorkflowSnapshot snapshot,
        Machining machining,
        WorkflowFeatureGroupKind kind)
    {
        return ClassifyWorkflowFeatureGroup(snapshot, machining) == kind;
    }

    private static WorkflowFeatureGroupKind ClassifyWorkflowFeatureGroup(
        PlateWorkflowSnapshot snapshot,
        Machining machining)
    {
        if (machining is DrillMachining or DrillPatternMachining or HorizontalDrillMachining)
            return WorkflowFeatureGroupKind.Drill;

        if (IsClosedContour(machining))
        {
            if (LooksLikeOutsideContour(machining.Name))
                return WorkflowFeatureGroupKind.OutsideContour;

            if (LooksLikeInsideContour(machining.Name))
                return WorkflowFeatureGroupKind.InsideContour;

            var outsideContour = FindFallbackOutsideContour(snapshot);
            return ReferenceEquals(outsideContour, machining)
                ? WorkflowFeatureGroupKind.OutsideContour
                : WorkflowFeatureGroupKind.InsideContour;
        }

        return WorkflowFeatureGroupKind.Other;
    }

    private static Machining? FindFallbackOutsideContour(PlateWorkflowSnapshot snapshot)
    {
        return snapshot.CombinedMachinings
            .Where(IsClosedContour)
            .OrderByDescending(GetContourArea)
            .FirstOrDefault();
    }

    private static bool IsClosedContour(Machining machining)
    {
        return machining switch
        {
            RoutingMachining routing => routing.IsClosed,
            RoutingWithArcsMachining routingWithArcs => routingWithArcs.IsClosed,
            _ => false
        };
    }

    private static bool LooksLikeOutsideContour(string name)
    {
        return name.Contains("aussen", StringComparison.OrdinalIgnoreCase)
            || name.Contains("außen", StringComparison.OrdinalIgnoreCase)
            || name.Contains("outside", StringComparison.OrdinalIgnoreCase)
            || name.Contains("outer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeInsideContour(string name)
    {
        return name.Contains("innen", StringComparison.OrdinalIgnoreCase)
            || name.Contains("inside", StringComparison.OrdinalIgnoreCase)
            || name.Contains("inner", StringComparison.OrdinalIgnoreCase);
    }

    private static double GetContourArea(Machining machining)
    {
        var points = machining switch
        {
            RoutingMachining routing => routing.Points,
            RoutingWithArcsMachining routingWithArcs => BuildRoutingWithArcsPoints(routingWithArcs),
            _ => Array.Empty<(double X, double Y)>()
        };

        if (points.Count < 3)
            return 0;

        double area = 0;
        for (var index = 0; index < points.Count; index++)
        {
            var current = points[index];
            var next = points[(index + 1) % points.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return Math.Abs(area) * 0.5;
    }

    private static IReadOnlyList<(double X, double Y)> BuildRoutingWithArcsPoints(RoutingWithArcsMachining routingWithArcs)
    {
        var points = new List<(double X, double Y)> { (routingWithArcs.StartX, routingWithArcs.StartY) };
        foreach (var segment in routingWithArcs.Segments)
        {
            points.Add((segment.EndX, segment.EndY));
        }

        return points;
    }

    private static string BuildWorkflowFeatureDetail(Machining machining)
    {
        return machining switch
        {
            DrillMachining drill => string.Create(
                CultureInfo.InvariantCulture,
                $"Ø {drill.Diameter:0.###} · Z {drill.Depth:0.###} · X {drill.X:0.###} / Y {drill.Y:0.###}"),
            DrillPatternMachining pattern => string.Create(
                CultureInfo.InvariantCulture,
                $"Ø {pattern.Diameter:0.###} · {pattern.CountX}×{pattern.CountY} · X {pattern.X:0.###} / Y {pattern.Y:0.###}"),
            HorizontalDrillMachining horizontal => string.Create(
                CultureInfo.InvariantCulture,
                $"Ø {horizontal.Diameter:0.###} · Z {horizontal.Depth:0.###} · Seite {horizontal.DrillSide}"),
            RoutingMachining routing => string.Create(
                CultureInfo.InvariantCulture,
                $"Z {routing.Depth:0.###} · Ø {routing.ToolDiameter:0.###} · {routing.Points.Count} Pt."),
            RoutingWithArcsMachining routingWithArcs => string.Create(
                CultureInfo.InvariantCulture,
                $"Z {routingWithArcs.Depth:0.###} · Ø {routingWithArcs.ToolDiameter:0.###} · {routingWithArcs.Segments.Count} Seg."),
            PocketMachining pocket => string.Create(
                CultureInfo.InvariantCulture,
                $"Pocket · Z {pocket.Depth:0.###} · Ø {pocket.ToolDiameter:0.###}"),
            GrooveRntMachining groove => string.Create(
                CultureInfo.InvariantCulture,
                $"RNT {groove.RntCode} · Z {groove.Depth:0.###} · W {groove.Width:0.###}"),
            MacroMachining macro => $"Makro · {macro.MacroName}",
            BladeCutMachining bladeCut => string.Create(
                CultureInfo.InvariantCulture,
                $"BladeCut · {bladeCut.Angle:0.###}° · Z {bladeCut.Depth:0.###}"),
            _ => ToolpathPlanner.GetMachiningType(machining).ToString()
        };
    }

    private static string BuildWorkflowSourceSummary(IEnumerable<string> sourceLabels)
    {
        var counts = sourceLabels
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => GetWorkflowSourceSortOrder(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}: {group.Count()}");

        return string.Join(" · ", counts);
    }

    private static string GetWorkflowSourceLabel(MachiningSource source)
    {
        return source switch
        {
            MachiningSource.BlockDetection => "Block-Ops",
            MachiningSource.FaceTag => "Face-Feature",
            MachiningSource.Manual => "Manuell",
            _ => "Legacy"
        };
    }

    private static string GetFeatureGroupDisplayName(WorkflowFeatureGroupKind kind)
    {
        return kind switch
        {
            WorkflowFeatureGroupKind.Drill => "Bohrungen",
            WorkflowFeatureGroupKind.InsideContour => "Innenkonturen",
            WorkflowFeatureGroupKind.OutsideContour => "Außenkontur",
            _ => "Weitere Workflow-Features"
        };
    }

    private static int GetWorkflowGroupSortOrder(WorkflowFeatureGroupKind kind)
    {
        return kind switch
        {
            WorkflowFeatureGroupKind.Drill => 0,
            WorkflowFeatureGroupKind.InsideContour => 1,
            WorkflowFeatureGroupKind.OutsideContour => 2,
            _ => 3
        };
    }

    private static int GetWorkflowSourceSortOrder(string sourceLabel)
    {
        return sourceLabel switch
        {
            "Block-Ops" => 0,
            "Face-Feature" => 1,
            "Manuell" => 2,
            _ => 3
        };
    }

    private void RefreshStrategySummary()
    {
        if (_strategyOverrides.Count == 0)
        {
            _strategySummaryLabel.Text = "Strategien: Auto-Heuristik";
            return;
        }

        _strategySummaryLabel.Text = $"Strategien: {_strategyOverrides.Count} Override(s) aktiv";
    }

    private static double ParseDoubleOrDefault(string? text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static string FormatMode(ExportMode mode)
    {
        return mode switch
        {
            ExportMode.LegacyOnly => "2D Legacy",
            ExportMode.MultiPlate3D => "3D Multi-Platte",
            _ => "Automatisch"
        };
    }

    private static string YesNo(bool value) => value ? "ja" : "nein";

    private string BuildReportText(DocumentExportResult result)
    {
        var lines = new List<string>
        {
            $"Modus: {FormatMode(result.ResolvedMode)}"
        };

        if (result.Report != null)
        {
            lines.Add(result.Report.SummaryLine);
            lines.Add($"Blöcke: {result.Report.TotalBlocks}");
        }
        else
        {
            lines.Add($"{result.ExportedFiles.Count} Datei(en) exportiert.");
        }

        if (result.Analysis != null)
            lines.Add(BuildWorkflowSummary(result.Analysis.Plates));

        if (result.ExportedFiles.Count > 0)
        {
            lines.Add("Dateien:");
            lines.AddRange(result.ExportedFiles.Select(file => Path.GetFileName(file)));
        }

        return string.Join(System.Environment.NewLine, lines);
    }

    private string BuildWorkflowSummary(IReadOnlyList<CorePlatePreview> previews)
    {
        var blockMachinings = previews.Sum(preview => preview.BlockMachiningCount);
        var faceFeatures = previews.Sum(preview => preview.FaceFeatureCount);
        var manualMachinings = previews.Sum(preview => preview.ManualMachiningCount);
        var planningContext = TryCreateWorkflowPlanningContext();
        var openGroupCount = 0;
        var readyGroupCount = 0;
        var hasKnownAssignmentStatus = false;

        foreach (var preview in previews)
        {
            var plateKey = BuildPlateSelectionKey(preview.Plate.Name, preview.Plate.LayerPath);
            if (!_latestWorkflowSnapshots.TryGetValue(plateKey, out var snapshot))
            {
                snapshot = new WorkflowSnapshotService().BuildSnapshot(RhinoDoc.ActiveDoc, preview.Plate, preview.Blocks);
            }

            foreach (var group in BuildWorkflowFeatureGroups(snapshot, planningContext))
            {
                if (!group.HasAssignmentStatus)
                {
                    continue;
                }

                hasKnownAssignmentStatus = true;
                if (group.OpenCount > 0)
                {
                    openGroupCount++;
                }
                else
                {
                    readyGroupCount++;
                }
            }
        }

        return WorkflowSummaryText.Format(
            blockMachinings,
            faceFeatures,
            manualMachinings,
            openGroupCount,
            readyGroupCount,
            hasKnownAssignmentStatus);
    }

    private void UpdateReport(string text)
    {
        _reportArea.Text = text + System.Environment.NewLine;
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logArea.Append($"[{timestamp}] {message}\n", true);
    }
}

internal enum WorkflowFeatureGroupKind
{
    None,
    Drill,
    InsideContour,
    OutsideContour,
    Other
}

internal sealed record WorkflowTreeSelection(
    string PlateKey,
    string PlateName,
    WorkflowFeatureGroupKind Kind);

internal sealed record WorkflowFeatureItemView(
    WorkflowFeatureGroupKind Kind,
    string Name,
    string Detail,
    string SourceLabel,
    string? AssignmentLabel,
    string MachiningTypeLabel,
    WorkflowAssignmentStatus AssignmentStatus);

internal sealed record WorkflowFeatureGroupView(
    WorkflowFeatureGroupKind Kind,
    string DisplayName,
    int TotalCount,
    int OpenCount,
    int OverrideCount,
    bool HasAssignmentStatus,
    string Summary,
    string? AssignmentSummary,
    string SourceSummary,
    IReadOnlyList<WorkflowFeatureItemView> Items);

internal sealed record WorkflowAssignmentStatus(
    bool IsReady,
    bool HasOverride,
    bool IsKnown,
    string? ItemLabel);

internal sealed record WorkflowPlanningContext(
    ToolLibrary ToolLibrary,
    ToolpathPlanningOptions Options);
