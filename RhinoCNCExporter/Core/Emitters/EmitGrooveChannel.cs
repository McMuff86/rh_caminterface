namespace RhinoCNCExporter.Core.Emitters;

public static class EmitGrooveChannel
{
    public static string Emit(string name)
    {
        return $"// GROOVE_CH: {name}\n";
    }
}
