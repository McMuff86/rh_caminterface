using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter.Commands;

public sealed class ExportXilogCommand : Command
{
    public override string EnglishName => "ExportXilog";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var dialog = new ExportDialog();
        dialog.ShowModal();
        return Result.Success;
    }
}
