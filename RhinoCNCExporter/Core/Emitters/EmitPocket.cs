namespace RhinoCNCExporter.Core.Emitters;

public static class EmitPocket
{
    public static string Emit(string name)
    {
        return $"// POCKET: {name}\n";
    }
}
