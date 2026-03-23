using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoCNCExporter.UI;

/// <summary>
/// Dockable export panel for RhinoCNCExporter.
/// Registered as a Rhino Panel via Panels.RegisterPanel.
/// Dark/neutral professional design matching Rhino's dark UI.
/// </summary>
[Guid("a1f4d2e3-9c87-4b5f-a6d1-3e8f7c2b4a90")]
public sealed class ExportPanel : Panel
{
    public static readonly Guid PanelId = typeof(ExportPanel).GUID;
    public const string PanelDisplayName = "RhinoCNC Export";

    // --- UI Controls ---
    private readonly DropDown _machineDropDown;
    private readonly Label _lpxLabel;
    private readonly Label _lpyLabel;
    private readonly Label _lpzLabel;
    private readonly ListBox _operationsListBox;
    private readonly TextBox _exportPathTextBox;
    private readonly TextBox _stepdownTextBox;
    private readonly TextBox _toleranceTextBox;
    private readonly TextBox _toolDiaTextBox;
    private readonly TextBox _zugabeXTextBox;
    private readonly TextBox _zugabeYTextBox;
    private readonly CheckBox _layerStepdownCheckBox;
    private readonly CheckBox _onlySelectionCheckBox;
    private readonly TextArea _logArea;

    // --- Colors for dark theme ---
    private static readonly Color BgDark = Color.FromArgb(45, 45, 48);
    private static readonly Color BgMedium = Color.FromArgb(56, 56, 60);
    private static readonly Color BgLight = Color.FromArgb(68, 68, 72);
    private static readonly Color FgText = Color.FromArgb(220, 220, 220);
    private static readonly Color AccentBlue = Color.FromArgb(0, 122, 204);
    private static readonly Color AccentGreen = Color.FromArgb(78, 154, 6);
    private static readonly Color BorderColor = Color.FromArgb(80, 80, 84);

    public ExportPanel()
    {
        BackgroundColor = BgDark;

        // --- Header ---
        var headerLabel = CreateLabel("RhinoCNC Export", 13, true);

        // --- Machine Selection ---
        _machineDropDown = new DropDown();
        _machineDropDown.Items.Add("SCM Maestro (Xilog)");
        _machineDropDown.Items.Add("Biesse (CIX) — coming soon");
        _machineDropDown.Items.Add("Homag (MPR) — coming soon");
        _machineDropDown.SelectedIndex = 0;
        _machineDropDown.DropDownClosed += OnMachineChanged;

        var machineSection = CreateSection("Maschine",
            _machineDropDown);

        // --- Workpiece Info ---
        _lpxLabel = CreateLabel("LPX: —", 10, false);
        _lpyLabel = CreateLabel("LPY: —", 10, false);
        _lpzLabel = CreateLabel("LPZ: —", 10, false);

        var refreshWpButton = new Button { Text = "↻ Aktualisieren", Height = 26 };
        refreshWpButton.Click += (_, _) => RefreshWorkpieceInfo();

        var wpLayout = new StackLayout
        {
            Spacing = 4,
            Items = { _lpxLabel, _lpyLabel, _lpzLabel, refreshWpButton }
        };
        var workpieceSection = CreateSection("Werkstück (WK_PIECE)", wpLayout);

        // --- Operations List ---
        _operationsListBox = new ListBox { Height = 120 };
        var refreshOpsButton = new Button { Text = "↻ Layer scannen", Height = 26 };
        refreshOpsButton.Click += (_, _) => RefreshOperations();

        var opsLayout = new StackLayout
        {
            Spacing = 4,
            Items = { _operationsListBox, refreshOpsButton }
        };
        var operationsSection = CreateSection("Operationen", opsLayout);

        // --- Settings ---
        _stepdownTextBox = new TextBox { PlaceholderText = "Stepdown (mm)", Text = "3.0" };
        _toleranceTextBox = new TextBox { PlaceholderText = "Toleranz (mm)", Text = "0.05" };
        _toolDiaTextBox = new TextBox { PlaceholderText = "Tool Ø (mm)", Text = "9.5" };
        _zugabeXTextBox = new TextBox { PlaceholderText = "Zugabe X (mm)", Text = "2.5" };
        _zugabeYTextBox = new TextBox { PlaceholderText = "Zugabe Y (mm)", Text = "2.5" };
        _layerStepdownCheckBox = new CheckBox { Text = "Layer-Stepdown (_Sxx)", Checked = false, TextColor = FgText };
        _onlySelectionCheckBox = new CheckBox { Text = "Nur selektierte Geometrie", Checked = false, TextColor = FgText };

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
            }
        };
        var settingsSection = CreateSection("Einstellungen", settingsLayout);

        // --- Export Path + Button ---
        _exportPathTextBox = new TextBox { PlaceholderText = "Export-Pfad...", ReadOnly = true };
        var browseButton = new Button { Text = "...", Width = 36, Height = 26 };
        browseButton.Click += (_, _) => BrowseExportPath();

        var pathRow = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { new StackLayoutItem(_exportPathTextBox, true), browseButton }
        };

        var exportButton = new Button { Text = "▶ EXPORT", Height = 36 };
        exportButton.Click += (_, _) => RunExport();

        var exportLayout = new StackLayout
        {
            Spacing = 6,
            Items = { pathRow, exportButton }
        };
        var exportSection = CreateSection("Export", exportLayout);

        // --- Log Area ---
        _logArea = new TextArea
        {
            ReadOnly = true,
            Height = 100,
            Font = new Font("Consolas", 9),
            Text = "Bereit.\n"
        };
        var logSection = CreateSection("Log", _logArea);

        // --- Main Layout ---
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
                    machineSection,
                    workpieceSection,
                    operationsSection,
                    settingsSection,
                    exportSection,
                    logSection
                }
            }
        };

        // Initial scan
        RefreshWorkpieceInfo();
        RefreshOperations();
    }

    // --- Section Builders ---

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

    // --- Actions ---

    private void OnMachineChanged(object? sender, EventArgs e)
    {
        if (_machineDropDown.SelectedIndex > 0)
        {
            Log($"⚠ {_machineDropDown.SelectedValue} ist noch nicht implementiert.");
            _machineDropDown.SelectedIndex = 0;
        }
    }

    private void RefreshWorkpieceInfo()
    {
        try
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                SetWorkpieceLabels("—", "—", "—");
                return;
            }

            // Find WK_PIECE layer
            var layer = doc.Layers.FindName("WK_PIECE");
            if (layer == null)
            {
                SetWorkpieceLabels("—", "—", "—");
                Log("⚠ Kein WK_PIECE Layer gefunden.");
                return;
            }

            // Get objects on WK_PIECE layer, find largest closed curve
            var objects = doc.Objects.FindByLayer(layer);
            Rhino.Geometry.BoundingBox bestBb = Rhino.Geometry.BoundingBox.Empty;
            bool found = false;

            foreach (var ro in objects)
            {
                if (ro.Geometry is Rhino.Geometry.Curve crv && crv.IsClosed)
                {
                    var bb = crv.GetBoundingBox(true);
                    if (!found || (bb.Max.X - bb.Min.X) * (bb.Max.Y - bb.Min.Y) >
                        (bestBb.Max.X - bestBb.Min.X) * (bestBb.Max.Y - bestBb.Min.Y))
                    {
                        bestBb = bb;
                        found = true;
                    }
                }
            }

            if (found)
            {
                double lpx = bestBb.Max.X - bestBb.Min.X;
                double lpy = bestBb.Max.Y - bestBb.Min.Y;
                double lpz = Core.LayerParser.Defaults.DefaultDz; // Use default DZ
                SetWorkpieceLabels(
                    lpx.ToString("F1", CultureInfo.InvariantCulture),
                    lpy.ToString("F1", CultureInfo.InvariantCulture),
                    lpz.ToString("F1", CultureInfo.InvariantCulture));
                Log($"Werkstück: {lpx:F1} × {lpy:F1} × {lpz:F1} mm");
            }
            else
            {
                SetWorkpieceLabels("—", "—", "—");
                Log("⚠ Keine geschlossene Kurve auf WK_PIECE.");
            }
        }
        catch (Exception ex)
        {
            Log($"❌ Fehler: {ex.Message}");
        }
    }

    private void SetWorkpieceLabels(string x, string y, string z)
    {
        _lpxLabel.Text = $"LPX: {x} mm";
        _lpyLabel.Text = $"LPY: {y} mm";
        _lpzLabel.Text = $"LPZ: {z} mm";
    }

    private void RefreshOperations()
    {
        try
        {
            _operationsListBox.Items.Clear();
            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (doc == null) return;

            int cutCount = 0, pocketCount = 0, drillCount = 0, rowCount = 0, grooveCount = 0, rntCount = 0;

            foreach (var layer in doc.Layers)
            {
                var name = layer.Name ?? "";
                if (string.IsNullOrWhiteSpace(name) || layer.IsDeleted) continue;

                if (Core.LayerParser.LayerRegex.TryParseCut(name, out var cutSpec))
                {
                    cutCount++;
                    _operationsListBox.Items.Add($"✂ CUT: {name} (Z={cutSpec!.Depth}, Ø{cutSpec.ToolDiameter})");
                }
                else if (Core.LayerParser.LayerRegex.TryParsePocket(name, out var pocketSpec))
                {
                    pocketCount++;
                    _operationsListBox.Items.Add($"▣ POCKET: {name} (Z={pocketSpec!.Depth}, Ø{pocketSpec.ToolDiameter})");
                }
                else if (Core.LayerParser.LayerRegex.TryParseDrill(name, out var drillSpec))
                {
                    drillCount++;
                    _operationsListBox.Items.Add($"● DRILL: {name} (Ø{drillSpec!.Diameter}, Z={drillSpec.Depth})");
                }
                else if (Core.LayerParser.LayerRegex.TryParseRow(name, out var rowSpec))
                {
                    rowCount++;
                    _operationsListBox.Items.Add($"●● ROW: {name} (Ø{rowSpec!.Diameter}, P={rowSpec.Pitch})");
                }
                else if (Core.LayerParser.LayerRegex.TryParseGrooveChannel(name, out var chSpec))
                {
                    grooveCount++;
                    _operationsListBox.Items.Add($"═ GROOVE: {name} (W={chSpec!.Width}, Z={chSpec.Depth})");
                }
                else if (Core.LayerParser.LayerRegex.TryParseGrooveRnt(name, out var rntSpec))
                {
                    rntCount++;
                    _operationsListBox.Items.Add($"═ RNT: {name} (W={rntSpec!.Width}, C={rntSpec.Code})");
                }
            }

            int total = cutCount + pocketCount + drillCount + rowCount + grooveCount + rntCount;
            Log($"Gefunden: {total} Operationen ({cutCount} CUT, {pocketCount} POCKET, {drillCount} DRILL, {rowCount} ROW, {grooveCount + rntCount} GROOVE)");
        }
        catch (Exception ex)
        {
            Log($"❌ Scan-Fehler: {ex.Message}");
        }
    }

    private void BrowseExportPath()
    {
        var doc = Rhino.RhinoDoc.ActiveDoc;
        string defaultName = "program.xcs";
        if (doc != null && !string.IsNullOrWhiteSpace(doc.Name))
            defaultName = Path.ChangeExtension(doc.Name, ".xcs");

        var dlg = new SaveFileDialog
        {
            Title = "Export-Pfad wählen",
            Filters = { new FileFilter("Xilog Script (*.xcs)", ".xcs") },
            FileName = defaultName
        };

        if (dlg.ShowDialog(this) == DialogResult.Ok && !string.IsNullOrWhiteSpace(dlg.FileName))
        {
            _exportPathTextBox.Text = dlg.FileName;
        }
    }

    private void RunExport()
    {
        try
        {
            // Validate machine
            if (_machineDropDown.SelectedIndex != 0)
            {
                Log("❌ Nur SCM Maestro ist aktuell unterstützt.");
                return;
            }

            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                Log("❌ Kein aktives Rhino-Dokument.");
                return;
            }

            // Get export path
            string path = _exportPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                BrowseExportPath();
                path = _exportPathTextBox.Text;
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            if (!path.ToLowerInvariant().EndsWith(".xcs"))
                path += ".xcs";

            bool layerStepdown = _layerStepdownCheckBox.Checked ?? false;
            bool onlySelection = _onlySelectionCheckBox.Checked ?? false;

            double zugabeX = 2.5, zugabeY = 2.5;
            if (double.TryParse(_zugabeXTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var zx)) zugabeX = zx;
            if (double.TryParse(_zugabeYTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var zy)) zugabeY = zy;

            Log($"Exportiere → {Path.GetFileName(path)} (Zugabe {zugabeX}/{zugabeY}mm) ...");

            bool ok = Services.ExportService.ExportXilog(doc, onlySelection, path, layerStepdown, zugabeX, zugabeY);

            if (ok)
            {
                Log($"✅ Export erfolgreich: {path}");
                Rhino.RhinoApp.WriteLine($"[RhinoCNCExporter] XCS erstellt: {path}");
            }
            else
            {
                Log("❌ Export fehlgeschlagen. Siehe Rhino-Konsole.");
            }
        }
        catch (Exception ex)
        {
            Log($"❌ Export-Fehler: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logArea.Append($"[{timestamp}] {message}\n", true);
    }
}
