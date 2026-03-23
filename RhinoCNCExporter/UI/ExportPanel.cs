using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Pipeline;
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
    private readonly ListBox _operationsListBox;
    private readonly TreeGridView _plateTreeView;
    private readonly TextBox _exportPathTextBox;
    private readonly TextBox _stepdownTextBox;
    private readonly TextBox _toleranceTextBox;
    private readonly TextBox _toolDiaTextBox;
    private readonly TextBox _zugabeXTextBox;
    private readonly TextBox _zugabeYTextBox;
    private readonly CheckBox _layerStepdownCheckBox;
    private readonly CheckBox _onlySelectionCheckBox;
    private readonly CheckBox _blockDetectionCheckBox;
    private readonly TextArea _reportArea;
    private readonly TextArea _logArea;

    private TreeGridItemCollection _plateTreeItems = new();
    private DocumentExportAnalysis? _latestAnalysis;

    private static readonly Color BgDark = Color.FromArgb(45, 45, 48);
    private static readonly Color FgText = Color.FromArgb(220, 220, 220);

    public ExportPanel()
    {
        BackgroundColor = BgDark;

        var headerLabel = CreateLabel("RhinoCNC Export", 13, true);

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
        var modeSection = CreateSection("Modus", modeLayout);

        _summaryLabel = CreateLabel("Dokument: —", 10, false);
        _recommendationLabel = CreateLabel("Empfehlung: —", 10, false);
        _capabilityLabel = CreateLabel("Capabilities: —", 9, false);
        var summarySection = CreateSection("Dokumentanalyse",
            new StackLayout
            {
                Spacing = 4,
                Items = { _summaryLabel, _recommendationLabel, _capabilityLabel }
            });

        _operationsListBox = new ListBox { Height = 110 };
        var refreshOpsButton = new Button { Text = "↻ Layer scannen", Height = 26 };
        refreshOpsButton.Click += (_, _) => RefreshOperations();
        var operationsSection = CreateSection("Legacy-Operationen",
            new StackLayout
            {
                Spacing = 4,
                Items = { _operationsListBox, refreshOpsButton }
            });

        _plateTreeView = CreatePlateTreeView();
        var scanPlatesButton = new Button { Text = "↻ 3D Vorschau", Height = 26 };
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

        var plateSection = CreateSection("Platten & Blöcke",
            new StackLayout
            {
                Spacing = 4,
                Items = { _plateTreeView, plateActions }
            });

        _stepdownTextBox = new TextBox { PlaceholderText = "Stepdown (mm)", Text = "3.0" };
        _toleranceTextBox = new TextBox { PlaceholderText = "Toleranz (mm)", Text = "0.05" };
        _toolDiaTextBox = new TextBox { PlaceholderText = "Tool Ø (mm)", Text = "9.5" };
        _zugabeXTextBox = new TextBox { PlaceholderText = "Zugabe X (mm)", Text = "2.5" };
        _zugabeYTextBox = new TextBox { PlaceholderText = "Zugabe Y (mm)", Text = "2.5" };
        _layerStepdownCheckBox = new CheckBox { Text = "Layer-Stepdown (_Sxx)", Checked = false, TextColor = FgText };
        _onlySelectionCheckBox = new CheckBox { Text = "Nur selektierte Geometrie", Checked = false, TextColor = FgText };
        _blockDetectionCheckBox = new CheckBox { Text = "Block-Detection aktivieren", Checked = true, TextColor = FgText };

        var settingsLayout = new TableLayout
        {
            Spacing = new Size(8, 4),
            Rows =
            {
                new TableRow(CreateLabel("Stepdown (mm):", 9, false), new TableCell(_stepdownTextBox, true)),
                new TableRow(CreateLabel("Toleranz (mm):", 9, false), new TableCell(_toleranceTextBox, true)),
                new TableRow(CreateLabel("Tool Ø (mm):", 9, false), new TableCell(_toolDiaTextBox, true)),
                new TableRow(CreateLabel("Zugabe X (mm):", 9, false), new TableCell(_zugabeXTextBox, true)),
                new TableRow(CreateLabel("Zugabe Y (mm):", 9, false), new TableCell(_zugabeYTextBox, true)),
                new TableRow(new TableCell(_layerStepdownCheckBox) { ScaleWidth = true }),
                new TableRow(new TableCell(_onlySelectionCheckBox) { ScaleWidth = true }),
                new TableRow(new TableCell(_blockDetectionCheckBox) { ScaleWidth = true })
            }
        };
        var settingsSection = CreateSection("Einstellungen", settingsLayout);

        _exportPathTextBox = new TextBox { PlaceholderText = "Export-Ziel...", ReadOnly = true };
        var browseButton = new Button { Text = "...", Width = 36, Height = 26 };
        browseButton.Click += (_, _) => BrowseExportTarget();

        var pathRow = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { new StackLayoutItem(_exportPathTextBox, true), browseButton }
        };

        var exportButton = new Button { Text = "▶ EXPORT", Height = 36 };
        exportButton.Click += (_, _) => RunExport();

        var exportSection = CreateSection("Export",
            new StackLayout
            {
                Spacing = 6,
                Items = { pathRow, exportButton }
            });

        _reportArea = new TextArea
        {
            ReadOnly = true,
            Height = 70,
            Font = new Font("Consolas", 9),
            Text = "Noch kein Export.\n"
        };
        var reportSection = CreateSection("Export-Report", _reportArea);

        _logArea = new TextArea
        {
            ReadOnly = true,
            Height = 100,
            Font = new Font("Consolas", 9),
            Text = "Bereit.\n"
        };
        var logSection = CreateSection("Log", _logArea);

        Content = new Scrollable
        {
            Border = BorderType.None,
            Content = new StackLayout
            {
                Padding = new Padding(8),
                Spacing = 6,
                Items =
                {
                    headerLabel,
                    modeSection,
                    summarySection,
                    operationsSection,
                    plateSection,
                    settingsSection,
                    exportSection,
                    reportSection,
                    logSection
                }
            }
        };

        RefreshOperations();
        RefreshDocumentAnalysis();
    }

    private static Label CreateLabel(string text, float size, bool bold)
    {
        return new Label
        {
            Text = text,
            TextColor = FgText,
            Font = bold ? new Font(SystemFont.Bold, size) : new Font(SystemFont.Default, size)
        };
    }

    private static GroupBox CreateSection(string title, Control content)
    {
        return new GroupBox
        {
            Text = title,
            TextColor = FgText,
            Padding = new Padding(8, 6),
            Content = content
        };
    }

    private static TreeGridView CreatePlateTreeView()
    {
        var view = new TreeGridView
        {
            Height = 220
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
                _summaryLabel.Text = "Dokument: —";
                _recommendationLabel.Text = "Empfehlung: —";
                _capabilityLabel.Text = "Capabilities: —";
                PopulatePlateTree(Array.Empty<PlatePreview>());
                return;
            }

            _latestAnalysis = ExportService3D.AnalyzeDocument(doc);
            var caps = _latestAnalysis.Capabilities;

            _summaryLabel.Text =
                $"Dokument: {_latestAnalysis.Plates.Count} Platte(n), {_latestAnalysis.TotalBlockCount} Block(s)";
            _recommendationLabel.Text =
                $"Empfehlung: {FormatMode(_latestAnalysis.RecommendedMode)}";
            _capabilityLabel.Text =
                $"Legacy={YesNo(caps.HasLegacyPiece)} · Legacy-Layer={YesNo(caps.HasLegacyMachiningLayers)} · 3D={YesNo(caps.Has3DPlates)} · Blocks={YesNo(caps.HasBlocks)}";

            PopulatePlateTree(_latestAnalysis.Plates);
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

    private void PopulatePlateTree(IReadOnlyList<PlatePreview> previews)
    {
        _plateTreeItems = new TreeGridItemCollection();

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
            var plateItem = new TreeGridItem
            {
                Values = new object?[]
                {
                    true,
                    preview.Plate.Name,
                    $"{preview.Plate.LengthX:F1} × {preview.Plate.WidthY:F1} × {preview.Plate.Thickness:F1}",
                    preview.Plate.LayerPath ?? preview.Plate.Source.ToString(),
                    preview.MachiningCount.ToString(CultureInfo.InvariantCulture)
                }
            };

            if (preview.Blocks.Count == 0)
            {
                plateItem.Children.Add(new TreeGridItem
                {
                    Values = new object?[]
                    {
                        null,
                        "(keine Blöcke)",
                        string.Empty,
                        string.Empty,
                        "0"
                    }
                });
            }
            else
            {
                foreach (var block in preview.Blocks.OrderBy(b => b.BlockName, StringComparer.OrdinalIgnoreCase))
                {
                    plateItem.Children.Add(new TreeGridItem
                    {
                        Values = new object?[]
                        {
                            null,
                            block.BlockName,
                            block.CncType,
                            block.LayerName ?? string.Empty,
                            MachiningCountForBlock(block).ToString(CultureInfo.InvariantCulture)
                        }
                    });
                }
            }

            _plateTreeItems.Add(plateItem);
        }

        _plateTreeView.DataStore = _plateTreeItems;
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
            var selectedPlateNames = new HashSet<string>(GetSelectedPlateNames(), StringComparer.OrdinalIgnoreCase);

            if (decision.ResolvedMode == ExportMode.MultiPlate3D && selectedPlateNames.Count == 0)
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
                selectedPlateNames);

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

    private IEnumerable<string> GetSelectedPlateNames()
    {
        foreach (var item in EnumerateRootItems())
        {
            var values = item.Values.Cast<object?>().ToList();
            var isSelected = values.Count > 0 && values[0] is bool selected && selected;
            var name = values.Count > 1 ? values[1] as string : null;
            if (isSelected && !string.IsNullOrWhiteSpace(name) && name != "Keine 3D-Platten erkannt")
            {
                yield return name;
            }
        }
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

    private static int MachiningCountForBlock(FittingBlock block)
    {
        if (block.CncType.Equals("MACRO", StringComparison.OrdinalIgnoreCase))
            return 1;

        return block.CncType.Equals("DRILLPATTERN", StringComparison.OrdinalIgnoreCase) ? 1 : 1;
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

        if (result.ExportedFiles.Count > 0)
        {
            lines.Add("Dateien:");
            lines.AddRange(result.ExportedFiles.Select(file => Path.GetFileName(file)));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void UpdateReport(string text)
    {
        _reportArea.Text = text + Environment.NewLine;
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logArea.Append($"[{timestamp}] {message}\n", true);
    }
}
