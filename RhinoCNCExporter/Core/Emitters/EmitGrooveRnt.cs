namespace RhinoCNCExporter.Core.Emitters;

public static class EmitGrooveRnt
{
    public static string Emit(string name)
    {
        return $"// GROOVE_RNT: {name}\n";
    }
}
