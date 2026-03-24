using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Profiles;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Persists one tool library per machine profile under the user's application-data folder.
/// </summary>
public sealed class ToolLibraryStore
{
    private static readonly string RootDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RhinoCNCExporter",
        "ToolLibraries");

    public string GetDefaultPath(IMachineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Path.Combine(RootDirectory, $"{profile.MachineKey}.json");
    }

    public ToolLibrary LoadOrCreate(IMachineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var path = GetDefaultPath(profile);
        if (File.Exists(path))
        {
            var defaults = ToolLibrary.CreateDefault(profile);
            var library = ToolLibrary.LoadFromFile(path).MergeDefaults(defaults);
            Save(profile, library);
            return library;
        }

        var defaultLibrary = ToolLibrary.CreateDefault(profile);
        defaultLibrary.SaveToFile(path);
        return defaultLibrary;
    }

    public ToolLibrary Import(IMachineProfile profile, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureNotBlank(sourcePath, nameof(sourcePath));

        var library = ToolLibrary.LoadFromFile(sourcePath).MergeDefaults(ToolLibrary.CreateDefault(profile)) with
        {
            MachineKey = profile.MachineKey
        };

        Save(profile, library);
        return library;
    }

    public void Save(IMachineProfile profile, ToolLibrary library)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(library);

        (library with { MachineKey = profile.MachineKey }).SaveToFile(GetDefaultPath(profile));
    }

    public void Export(string destinationPath, ToolLibrary library)
    {
        EnsureNotBlank(destinationPath, nameof(destinationPath));
        ArgumentNullException.ThrowIfNull(library);

        library.SaveToFile(destinationPath);
    }

    public ToolLibrary ResetToDefaults(IMachineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var library = ToolLibrary.CreateDefault(profile);
        Save(profile, library);
        return library;
    }

    private static void EnsureNotBlank(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace.", paramName);
    }
}
