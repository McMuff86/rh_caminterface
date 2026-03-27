using System;
using System.Collections.Generic;
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
/// Right side: read-only CNC code preview (monospace).
/// User confirms with "Exportieren" or cancels.
/// </summary>
public sealed class ExportPreviewDialog : Dialog<bool>
{
    private readonly ListBox _plateListBox;
    private readonly TextArea _codePreview;
    private readonly Label _plateInfoLabel;
    private readonly Label _summaryLabel;

    private readonly IReadOnlyList<PlatePreview> _plates;

    public ExportPreviewDialog(IReadOnlyList<PlatePreview> plates)
    {
        _plates = plates;

        Title = "CNC Export — Vorschau";
        MinimumSize = new Size(900, 600);
        Resizable = true;
        Padding = new Padding(12);

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

        // --- Right side: Code Preview ---
        _codePreview = new TextArea
        {
            ReadOnly = true,
            Wrap = false,
            Font = new Font("Consolas, Courier New, monospace", 9),
            Text = "← Platte auswählen, um CNC-Code anzuzeigen",
        };

        var rightPanel = new StackLayout
        {
            Spacing = 8,
            Items =
            {
                new Label
                {
                    Text = "📄 CNC-Code Vorschau",
                    Font = new Font(SystemFont.Bold, 11),
                },
                new StackLayoutItem(_codePreview, true)
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
            Spacing = 10,
            Items =
            {
                new StackLayoutItem(splitter, true),
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

    private void OnPlateSelected(object? sender, EventArgs e)
    {
        var idx = _plateListBox.SelectedIndex;
        if (idx < 0 || idx >= _plates.Count)
        {
            _codePreview.Text = "";
            _plateInfoLabel.Text = "";
            return;
        }

        var plate = _plates[idx];

        // Update info label
        _plateInfoLabel.Text =
            $"Abmessungen: {plate.LengthX:F1} × {plate.WidthY:F1} × {plate.Thickness:F1} mm\n" +
            $"Operationen: {plate.OperationCount}\n" +
            $"Code-Zeilen: {plate.Code.Split('\n').Length}";

        // Update code preview
        _codePreview.Text = plate.Code;
        _codePreview.CaretIndex = 0;
    }

    /// <summary>
    /// Shows the dialog modally and returns true if user confirmed export.
    /// </summary>
    public bool ShowModalOnTop()
    {
        return ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
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
