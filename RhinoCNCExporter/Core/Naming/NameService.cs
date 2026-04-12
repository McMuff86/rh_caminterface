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
    private readonly object _sync = new();

    public NameService(int maxLength = 31)
    {
        _maxLen = maxLength;
    }

    /// <summary>
    /// Get a unique name, sanitized and truncated to max length.
    /// Matches Python's UniqueNames.get() logic exactly.
    /// </summary>
    public string CreateUnique(string baseName) => CreateUniqueInternal(baseName, Sanitize);

    /// <summary>
    /// Get a unique name while preserving user-facing characters like spaces or slashes.
    /// Only characters that would break XCS string literals are replaced.
    /// Useful for production-facing operation names where Maestro output should remain human-readable.
    /// </summary>
    public string CreateUniquePreservingText(string baseName) => CreateUniqueInternal(baseName, PreserveText);

    /// <summary>
    /// Sanitize: replace non-alphanumeric/underscore with '_', trim to max length.
    /// Matches Python's UniqueNames.sanitize() exactly.
    /// </summary>
    private string Sanitize(string s)
    {
        s = SanitizeRegex.Replace(s, "_");
        return TrimToMaxLength(s);
    }

    private string PreserveText(string s)
    {
        s = (s ?? string.Empty)
            .Replace('"', '_')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return TrimToMaxLength(s);
    }

    private string TrimToMaxLength(string s)
    {
        return s.Length > _maxLen ? s[.._maxLen] : s;
    }

    private string CreateUniqueInternal(string baseName, Func<string, string> normalize)
    {
        lock (_sync)
        {
            var @base = normalize(baseName ?? string.Empty);

            if (!_used.Contains(@base) && _counts.GetValueOrDefault(@base, 0) == 0)
            {
                _used.Add(@base);
                _counts[@base] = 1;
                return @base;
            }

            var n = _counts.GetValueOrDefault(@base, 1) + 1;
            while (true)
            {
                var candidate = CreateSuffixedCandidate(@base, n);
                if (!_used.Contains(candidate))
                {
                    _used.Add(candidate);
                    _counts[@base] = n;
                    return candidate;
                }

                n++;
            }
        }
    }

    private string CreateSuffixedCandidate(string sanitizedBase, int index)
    {
        var suffix = $"_{index}";
        if (_maxLen <= suffix.Length)
            return suffix[^_maxLen..];

        var maxBaseLength = _maxLen - suffix.Length;
        var trimmedBase = sanitizedBase.Length > maxBaseLength
            ? sanitizedBase[..maxBaseLength]
            : sanitizedBase;

        return trimmedBase + suffix;
    }
}
