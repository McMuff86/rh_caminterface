using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RhinoCNCExporter.Core.Naming;

/// <summary>
/// Generates unique names with Maestro's 31-char limit.
/// Matches UniqueNames class from Python reference exactly.
/// </summary>
public sealed class NameService
{
    private static readonly Regex SanitizeRegex = new(@"[^A-Za-z0-9_]", RegexOptions.Compiled);

    private readonly HashSet<string> _used = new();
    private readonly Dictionary<string, int> _counts = new();
    private readonly int _maxLen;

    public NameService(int maxLength = 31)
    {
        _maxLen = maxLength;
    }

    /// <summary>
    /// Get a unique name, sanitized and truncated to max length.
    /// Matches Python's UniqueNames.get() logic exactly.
    /// </summary>
    public string CreateUnique(string baseName)
    {
        var @base = Sanitize(baseName);

        // First use — no suffix needed
        if (!_used.Contains(@base) && _counts.GetValueOrDefault(@base, 0) == 0)
        {
            _used.Add(@base);
            _counts[@base] = 1;
            return @base;
        }

        // Collision — append _N suffix
        int n = _counts.GetValueOrDefault(@base, 1) + 1;
        while (true)
        {
            var candidate = Sanitize($"{@base}_{n}");
            if (!_used.Contains(candidate))
            {
                _used.Add(candidate);
                _counts[@base] = n;
                return candidate;
            }
            n++;
        }
    }

    /// <summary>
    /// Sanitize: replace non-alphanumeric/underscore with '_', trim to max length.
    /// Matches Python's UniqueNames.sanitize() exactly.
    /// </summary>
    private string Sanitize(string s)
    {
        s = SanitizeRegex.Replace(s, "_");
        return s.Length > _maxLen ? s[.._maxLen] : s;
    }
}
