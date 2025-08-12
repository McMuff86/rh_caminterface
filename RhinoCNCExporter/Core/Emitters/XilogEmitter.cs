using System.Text;

namespace RhinoCNCExporter.Core.Emitters;

public static class XilogEmitter
{
    public static string EmitHeader(string programName)
    {
        var safe = programName.Replace(' ', '_');
        var sb = new StringBuilder();
        sb.AppendLine($"PROC {safe}");
        return sb.ToString();
    }

    public static string EmitFooter()
    {
        return "ENDPROC\n";
    }
}
