using System.IO;
using Rhino;
using Rhino.Commands;
using Eto.Forms;
using RhinoCNCExporter.Services;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter.Commands;

public sealed class ExportXilogCommand : Command
{
    public override string EnglishName => "ExportXilog";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Step 1: Options dialog (stepdown mode + selection)
        var dialog = new ExportDialog();
        if (!dialog.ShowModal())
            return Result.Cancel;

        // Step 2: Save file dialog
        var saveDlg = new SaveFileDialog
        {
            Title = "Speichere XCS",
            Filters = { new FileFilter("Xilog Script (*.xcs)", ".xcs") },
            FileName = string.IsNullOrWhiteSpace(doc.Name)
                ? "program.xcs"
                : Path.ChangeExtension(doc.Name, ".xcs")
        };

        if (saveDlg.ShowDialog(null) != DialogResult.Ok || string.IsNullOrWhiteSpace(saveDlg.FileName))
            return Result.Cancel;

        var filePath = saveDlg.FileName;
        if (!filePath.ToLowerInvariant().EndsWith(".xcs"))
            filePath += ".xcs";

        // Step 3: Export
        bool ok = ExportService.ExportXilog(doc, dialog.OnlySelected, filePath, dialog.UseLayerStepdown);
        if (ok)
        {
            RhinoApp.WriteLine($"[RhinoCNCExporter] XCS erstellt: {filePath}");
        }
        else
        {
            RhinoApp.WriteLine("[RhinoCNCExporter] Export fehlgeschlagen.");
            return Result.Failure;
        }

        return Result.Success;
    }
}
