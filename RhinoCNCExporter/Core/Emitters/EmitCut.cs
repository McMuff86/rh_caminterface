namespace RhinoCNCExporter.Core.Emitters;

public static class EmitCut
{
    public static string Emit(string name)
    {
        return $"// CUT: {name}\n";
    }
}
