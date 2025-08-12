namespace RhinoCNCExporter.Core.Emitters;

public static class EmitDrill
{
    public static string Emit(string name)
    {
        return $"// DRILL: {name}\n";
    }
}
