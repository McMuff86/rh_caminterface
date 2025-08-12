namespace RhinoCNCExporter.Core.Emitters;

public static class EmitRow
{
    public static string Emit(string name)
    {
        return $"// DRILLROW: {name}\n";
    }
}
