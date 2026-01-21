using System.IO;

namespace SkillManager.Services;

public static class PathUtilities
{
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(fullPath);

            if (!string.IsNullOrWhiteSpace(root))
            {
                var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(trimmed, trimmedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return trimmedRoot + Path.DirectorySeparatorChar;
                }
            }

            return trimmed;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static List<string> NormalizePaths(IEnumerable<string> paths)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var normalized = NormalizePath(path);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                set.Add(normalized);
            }
        }

        return set.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool IsPathUnder(string path, IReadOnlyCollection<string> normalizedRoots)
    {
        if (normalizedRoots.Count == 0) return false;

        var normalized = NormalizePath(path);
        if (string.IsNullOrEmpty(normalized)) return false;

        foreach (var root in normalizedRoots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;

            if (string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var prefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
