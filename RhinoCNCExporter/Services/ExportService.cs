using System;
using System.IO;
using System.Linq;
using System.Text;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Services;

public static class ExportService
{
    public static bool ExportXilog(RhinoDoc doc, bool onlySelection, string filePath)
    {
        try
        {
            var programNameBase = string.IsNullOrWhiteSpace(doc.Name) ? "Program" : Path.GetFileNameWithoutExtension(doc.Name);
            var nameService = new NameService(maxLength: 31);
            var programName = nameService.CreateUnique(programNameBase);

            var sb = new StringBuilder();
            sb.Append(XilogEmitter.EmitHeader(programName));

            var objects = onlySelection
                ? doc.Objects.GetSelectedObjects(includeLights: false, includeGrips: false)
                : doc.Objects.GetObjectList(ObjectType.Curve).ToArray();

            foreach (var obj in objects)
            {
                if (obj is not RhinoObject ro)
                    continue;

                var layerIndex = ro.Attributes.LayerIndex;
                var layerName = doc.Layers.FindIndex(layerIndex)?.Name ?? string.Empty;

                if (string.IsNullOrWhiteSpace(layerName))
                    continue;

                if (LayerRegex.TryParseCut(layerName, out var cut))
                {
                    var opName = nameService.CreateUnique($"CUT_{cut!.Tech}");
                    sb.Append(EmitCut.Emit(opName));
                    continue;
                }

                if (LayerRegex.TryParsePocket(layerName, out var pocket))
                {
                    var opName = nameService.CreateUnique($"POCKET_{pocket!.Tech}");
                    sb.Append(EmitPocket.Emit(opName));
                    continue;
                }

                if (LayerRegex.TryParseDrill(layerName, out var drill))
                {
                    var opName = nameService.CreateUnique($"DRILL_D{drill!.Diameter}");
                    sb.Append(EmitDrill.Emit(opName));
                    continue;
                }

                if (LayerRegex.TryParseRow(layerName, out var row))
                {
                    var opName = nameService.CreateUnique($"ROW_D{row!.Diameter}");
                    sb.Append(EmitRow.Emit(opName));
                    continue;
                }

                if (LayerRegex.TryParseGrooveChannel(layerName, out var gch))
                {
                    var opName = nameService.CreateUnique($"GROOVE_CH_{gch!.Axis}");
                    sb.Append(EmitGrooveChannel.Emit(opName));
                    continue;
                }

                if (LayerRegex.TryParseGrooveRnt(layerName, out var grnt))
                {
                    var opName = nameService.CreateUnique($"GROOVE_RNT_{grnt!.Axis}");
                    sb.Append(EmitGrooveRnt.Emit(opName));
                    continue;
                }

                // TODO: add POCKET/DRILL/ROW/GROOVE parsing and emission
            }

            sb.Append(XilogEmitter.EmitFooter());

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
