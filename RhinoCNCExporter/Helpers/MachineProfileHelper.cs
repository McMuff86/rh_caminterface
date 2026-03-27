using RhinoCNCExporter.Core.Profiles;

namespace RhinoCNCExporter.Helpers;

/// <summary>
/// Resolves a machine profile instance from a machine key string.
/// Used by CNCAdd* commands to get the correct profile from document settings.
/// </summary>
public static class MachineProfileHelper
{
    /// <summary>
    /// Resolves an <see cref="IMachineProfile"/> from the given machine key.
    /// Returns ScmProfile for "xilog"/"maestrocadt", BiesseProfile for "biesse".
    /// Falls back to ScmProfile if key is unrecognized.
    /// </summary>
    public static IMachineProfile ResolveProfile(string machineKey)
    {
        var normalized = (machineKey ?? "xilog").Trim().ToLowerInvariant();

        if (normalized.Contains("biesse"))
            return new BiesseProfile();

        if (normalized.Contains("maestro"))
            return new MaestroCadTProfile();

        return new ScmProfile();
    }
}
