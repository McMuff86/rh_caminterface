using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.UI;

/// <summary>
/// Modal dialog shown BEFORE writing CNC files.
/// Left side: plate list with dimensions and operation count.
/// Right side: CNC code preview with line numbers, monospace font, and syntax highlighting.
/// Includes Copy and Open-in-Editor buttons.
/// </summary>
public sealed class ExportPreviewDialog : Dialog<bool>
{
    private readonly ListBox _plateListBox;
    private readonly Drawable _codeDrawable;
    private readonly Scrollable _codeScrollable;
    private readonly Label _plateInfoLabel;
    private readonly Label _summaryLabel;

    private readonly IReadOnlyList<PlatePreview> _plates;

    // Parsed lines for syntax-highlighted rendering
    private List<CodeLine> _currentLines = new();
    private string _currentCode = string.Empty;

    // Fonts and colors for syntax highlighting
    private readonly Font _codeFont;
    private readonly Font _lineNumberFont;

    private static readonly Color ColorComment = Color.FromArgb(106, 153, 85);    // Green
    private static readonly Color ColorGCode = Color.FromArgb(86, 156, 214);      // Blue  
    private static readonly Color ColorMCode = Color.FromArgb(206, 145, 75);      // Orange
    private static readonly Color ColorDefault = Color.FromArgb(212, 212, 212);   // Light gray
    private static readonly Color ColorLineNumber = Color.FromArgb(110, 110, 110); // Dim gray
    private static readonly Color ColorBackground = Color.FromArgb(30, 30, 30);   // Dark background
    private static readonly Color ColorLineNumBg = Color.FromArgb(40, 40, 40);    // Slightly lighter for gutter

    private const float LineHeight = 16f;
    private const float LineNumberWidth = 55f;
    private const float CodeLeftPadding = 8f;

    public ExportPreviewDialog(IReadOnlyList<PlatePreview> plates)
    {
        _plates = plates;

        Title = "CNC Export — Vorschau";
        MinimumSize = new Size(950, 650);
        Resizable = true;
        Padding = new Padding(12);

        // Monospace font — prefer Consolas, fallback to Courier New
        _codeFont = new Font("Consolas", 9.5f);
        _lineNumberFont = new Font("Consolas", 9f);

        // --- Left side: Plate List ---
        _plateListBox = new ListBox
        {
            Width = 260,
        };

        foreach (var plate in plates)
        {
            _plateListBox.Items.Add(new ListItem
            {
                Text = $"{plate.Name}  ({plate.OperationCount} Op.)",
                Key = plate.Name
            });
        }

        _plateListBox.SelectedIndexChanged += OnPlateSelected;

        _plateInfoLabel = new Label
        {
            TextColor = Color.FromArgb(160, 160, 160),
            Font = new Font(SystemFont.Default, 9),
            Wrap = WrapMode.Word,
            Text = "Platte auswählen für Details…"
        };

        var leftPanel = new StackLayout
        {
            Spacing = 8,
            Width = 270,
            Items =
            {
                new Label
                {
                    Text = "📋 Platten",
                    Font = new Font(SystemFont.Bold, 11),
                },
                new StackLayoutItem(_plateListBox, true),
                _plateInfoLabel
            }
        };

        // --- Right side: Code Preview with Syntax Highlighting ---
        _codeDrawable = new Drawable
        {
            BackgroundColor = ColorBackground,
        };
        _codeDrawable.Paint += OnPaintCode;

        _codeScrollable = new Scrollable
        {
            Content = _codeDrawable,
            Border = BorderType.Line,
            ExpandContentWidth = false,
            ExpandContentHeight = false,
            BackgroundColor = ColorBackground,
        };

        // Toolbar above code
        var copyButton = new Button { Text = "📋 Kopieren", ToolTip = "CNC-Code in die Zwischenablage kopieren" };
        copyButton.Click += OnCopyToClipboard;

        var openButton = new Button { Text = "📂 In Datei öffnen", ToolTip = "Code in temporäre Datei schreiben und im Standard-Editor öffnen" };
        openButton.Click += OnOpenInEditor;

        var codeToolbar = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Items =
            {
                new Label
                {
                    Text = "📄 CNC-Code Vorschau",
                    Font = new Font(SystemFont.Bold, 11),
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new StackLayoutItem(null, true), // spacer
                copyButton,
                openButton,
            }
        };

        var rightPanel = new StackLayout
        {
            Spacing = 6,
            Items =
            {
                codeToolbar,
                new StackLayoutItem(_codeScrollable, true),
            }
        };

        // --- Legend ---
        var legendPanel = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Items =
            {
                CreateLegendItem("Kommentar", ColorComment),
                CreateLegendItem("G-Code", ColorGCode),
                CreateLegendItem("M-Code", ColorMCode),
                CreateLegendItem("Standard", ColorDefault),
            }
        };

        // --- Summary line ---
        var totalOps = plates.Sum(p => p.OperationCount);
        var totalLines = plates.Sum(p => p.Code.Split('\n').Length);
        _summaryLabel = new Label
        {
            Text = $"{plates.Count} Platte(n) | {totalOps} Operationen | ~{totalLines} Zeilen CNC-Code",
            Font = new Font(SystemFont.Default, 9),
            TextColor = Color.FromArgb(160, 160, 160),
        };

        // --- Buttons ---
        var exportButton = new Button { Text = "📤 Exportieren" };
        exportButton.Click += (_, _) => Close(true);

        var cancelButton = new Button { Text = "Abbrechen" };
        cancelButton.Click += (_, _) => Close(false);

        DefaultButton = exportButton;
        AbortButton = cancelButton;

        var buttonRow = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Items =
            {
                new StackLayoutItem(null, true), // spacer
                cancelButton,
                exportButton
            }
        };

        // --- Main Layout ---
        var splitter = new Splitter
        {
            Panel1 = leftPanel,
            Panel2 = rightPanel,
            Position = 280,
            Orientation = Orientation.Horizontal,
        };

        Content = new StackLayout
        {
            Spacing = 8,
            Items =
            {
                new StackLayoutItem(splitter, true),
                legendPanel,
                _summaryLabel,
                buttonRow
            }
        };

        // Auto-select first plate
        if (plates.Count > 0)
        {
            _plateListBox.SelectedIndex = 0;
        }
    }

    private static StackLayout CreateLegendItem(string label, Color color)
    {
        return new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items =
            {
                new Drawable
                {
                    Width = 12,
                    Height = 12,
                    // Simple colored square
                }.Apply(d => d.Paint += (_, e) =>
                {
                    e.Graphics.FillRectangle(color, 0, 0, 12, 12);
                }),
                new Label
                {
                    Text = label,
                    Font = new Font(SystemFont.Default, 8),
                    TextColor = Color.FromArgb(160, 160, 160),
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }

    private void OnPlateSelected(object? sender, EventArgs e)
    {
        var idx = _plateListBox.SelectedIndex;
        if (idx < 0 || idx >= _plates.Count)
        {
            _currentLines.Clear();
            _currentCode = string.Empty;
            _codeDrawable.Invalidate();
            _plateInfoLabel.Text = "";
            return;
        }

        var plate = _plates[idx];

        // Update info label
        _plateInfoLabel.Text =
            $"Abmessungen: {plate.LengthX:F1} × {plate.WidthY:F1} × {plate.Thickness:F1} mm\n" +
            $"Operationen: {plate.OperationCount}\n" +
            $"Code-Zeilen: {plate.Code.Split('\n').Length}";

        // Parse code for syntax highlighting
        _currentCode = plate.Code;
        _currentLines = ParseCodeLines(plate.Code);

        // Resize drawable to fit content
        var lineCount = _currentLines.Count;
        var maxLineWidth = _currentLines.Count > 0
            ? _currentLines.Max(l => l.Text.Length) * 7.5f + LineNumberWidth + CodeLeftPadding + 20f
            : 400f;
        _codeDrawable.Size = new Size(
            Math.Max((int)maxLineWidth, 600),
            Math.Max((int)(lineCount * LineHeight + 10), 400)
        );

        _codeDrawable.Invalidate();
    }

    private void OnPaintCode(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var bounds = _codeDrawable.Size;

        // Background
        g.FillRectangle(ColorBackground, 0, 0, bounds.Width, bounds.Height);

        // Line number gutter background
        g.FillRectangle(ColorLineNumBg, 0, 0, LineNumberWidth, bounds.Height);

        if (_currentLines.Count == 0)
        {
            g.DrawText(_codeFont, ColorDefault, LineNumberWidth + CodeLeftPadding, 8,
                "← Platte auswählen, um CNC-Code anzuzeigen");
            return;
        }

        float y = 4f;
        for (int i = 0; i < _currentLines.Count; i++)
        {
            var line = _currentLines[i];

            // Line number (right-aligned in gutter)
            var lineNum = (i + 1).ToString();
            var lineNumWidth = g.MeasureString(_lineNumberFont, lineNum).Width;
            g.DrawText(_lineNumberFont, ColorLineNumber,
                LineNumberWidth - lineNumWidth - 6f, y, lineNum);

            // Separator line
            g.DrawLine(new Pen(Color.FromArgb(60, 60, 60)), LineNumberWidth, y, LineNumberWidth, y + LineHeight);

            // Code text with syntax color
            g.DrawText(_codeFont, line.Color, LineNumberWidth + CodeLeftPadding, y, line.Text);

            y += LineHeight;
        }
    }

    private static List<CodeLine> ParseCodeLines(string code)
    {
        var lines = new List<CodeLine>();
        foreach (var rawLine in code.Split('\n'))
        {
            var text = rawLine.TrimEnd('\r');
            var color = ClassifyLine(text);
            lines.Add(new CodeLine(text, color));
        }
        return lines;
    }

    /// <summary>
    /// Classify a CNC code line for syntax highlighting:
    /// - Comments (starting with ; or ( ) → green
    /// - G-codes (G0, G1, G2, G3, etc.) → blue
    /// - M-codes (M0, M3, M5, M30, etc.) → orange
    /// - Everything else → default gray
    /// </summary>
    private static Color ClassifyLine(string line)
    {
        var trimmed = line.TrimStart();

        // Empty lines
        if (string.IsNullOrWhiteSpace(trimmed))
            return ColorDefault;

        // Comments: lines starting with ; or ( or //
        if (trimmed.StartsWith(';') || trimmed.StartsWith('(') || trimmed.StartsWith("//"))
            return ColorComment;

        // XCS-style comments/headers starting with ' or REM
        if (trimmed.StartsWith('\'') || trimmed.StartsWith("REM ", StringComparison.OrdinalIgnoreCase))
            return ColorComment;

        // M-codes: lines containing M followed by digits (check before G-codes as M is less common)
        if (ContainsMCode(trimmed))
            return ColorMCode;

        // G-codes: lines containing G followed by digits
        if (ContainsGCode(trimmed))
            return ColorGCode;

        // XCS-specific: CreateXxx(...) calls are like G-codes (operation definitions)
        if (trimmed.StartsWith("Create", StringComparison.Ordinal) ||
            trimmed.StartsWith("Add", StringComparison.Ordinal) ||
            trimmed.StartsWith("Set", StringComparison.Ordinal))
            return ColorGCode;

        return ColorDefault;
    }

    private static bool ContainsGCode(string line)
    {
        for (int i = 0; i < line.Length - 1; i++)
        {
            if ((line[i] == 'G' || line[i] == 'g') && char.IsDigit(line[i + 1]))
            {
                // Make sure G is at start or preceded by space/tab
                if (i == 0 || line[i - 1] == ' ' || line[i - 1] == '\t' || line[i - 1] == 'N')
                    return true;
            }
        }
        return false;
    }

    private static bool ContainsMCode(string line)
    {
        for (int i = 0; i < line.Length - 1; i++)
        {
            if ((line[i] == 'M' || line[i] == 'm') && char.IsDigit(line[i + 1]))
            {
                if (i == 0 || line[i - 1] == ' ' || line[i - 1] == '\t')
                    return true;
            }
        }
        return false;
    }

    private void OnCopyToClipboard(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentCode))
        {
            RhinoApp.WriteLine("Kein Code zum Kopieren vorhanden.");
            return;
        }

        try
        {
            var clipboard = new Clipboard();
            clipboard.Text = _currentCode;
            RhinoApp.WriteLine($"✅ {_currentLines.Count} Zeilen in die Zwischenablage kopiert.");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"⚠ Kopieren fehlgeschlagen: {ex.Message}");
        }
    }

    private void OnOpenInEditor(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentCode))
        {
            RhinoApp.WriteLine("Kein Code zum Öffnen vorhanden.");
            return;
        }

        try
        {
            var idx = _plateListBox.SelectedIndex;
            var plateName = idx >= 0 && idx < _plates.Count ? _plates[idx].Name : "preview";
            // Sanitize filename
            var safeName = string.Join("_", plateName.Split(Path.GetInvalidFileNameChars()));

            var tempDir = Path.Combine(Path.GetTempPath(), "RhinoCNC_Preview");
            Directory.CreateDirectory(tempDir);

            var tempFile = Path.Combine(tempDir, $"{safeName}.nc");
            File.WriteAllText(tempFile, _currentCode);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true
            });

            RhinoApp.WriteLine($"📂 Datei geöffnet: {tempFile}");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"⚠ Datei öffnen fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows the dialog modally and returns true if user confirmed export.
    /// </summary>
    public bool ShowModalOnTop()
    {
        return ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
    }

    private record struct CodeLine(string Text, Color Color);
}

/// <summary>Extension for fluent Drawable configuration.</summary>
internal static class DrawableExtensions
{
    public static T Apply<T>(this T control, Action<T> action)
    {
        action(control);
        return control;
    }
}

/// <summary>
/// Preview data for one plate including generated CNC code.
/// </summary>
public class PlatePreview
{
    public required string Name { get; init; }
    public double LengthX { get; init; }
    public double WidthY { get; init; }
    public double Thickness { get; init; }
    public int OperationCount { get; init; }
    public required string Code { get; init; }
}
