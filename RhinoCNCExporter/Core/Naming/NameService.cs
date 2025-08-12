using System.Collections.Generic;

namespace RhinoCNCExporter.Core.Naming;

public sealed class NameService
{
    private readonly HashSet<string> usedNames = new();
    private readonly int maxLength;

    public NameService(int maxLength = 31)
    {
        this.maxLength = maxLength;
    }

    public string CreateUnique(string baseName)
    {
        var sanitized = Sanitize(baseName);
        var name = TrimToMaxInternal(sanitized);
        if (usedNames.Add(name))
            return name;

        int i = 1;
        while (true)
        {
            var suffix = $"_{i}";
            var baseTrimmed = sanitized;
            if (sanitized.Length + suffix.Length > maxLength)
            {
                baseTrimmed = sanitized[..Math.Max(0, maxLength - suffix.Length)];
            }
            var candidate = baseTrimmed + suffix;
            if (usedNames.Add(candidate))
                return candidate;
            i++;
        }
    }

    private string Sanitize(string s)
    {
        // Minimal sanitization; extend as needed
        return s.Replace(' ', '_');
    }

    private string TrimToMaxInternal(string s)
    {
        return s.Length <= maxLength ? s : s[..maxLength];
    }
}
