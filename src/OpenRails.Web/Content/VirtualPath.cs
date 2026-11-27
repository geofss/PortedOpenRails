namespace OpenRails.Web.Content;

/// <summary>
/// Normalizes MSTS/Open Rails logical content paths independently of the host OS.
/// </summary>
public static class VirtualPath
{
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var candidate = path.Replace('\\', '/');
        if (candidate.StartsWith('/') || candidate.Contains(':'))
            throw new ArgumentException("Rooted and drive-qualified paths are not valid virtual paths.", nameof(path));

        var segments = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            throw new ArgumentException("A virtual path must identify a file or directory.", nameof(path));

        if (segments.Any(segment => segment is "." or ".."))
            throw new ArgumentException("Relative traversal segments are not valid in a normalized virtual path.", nameof(path));

        return string.Join('/', segments);
    }

    /// <summary>
    /// Resolves an include path relative to the file containing the include.
    /// Parent segments are allowed only while they remain inside the virtual root.
    /// </summary>
    public static string ResolveRelative(string containingFilePath, string includePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containingFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(includePath);

        var include = includePath.Replace('\\', '/');
        if (include.StartsWith('/') || include.Contains(':'))
            throw new ArgumentException("Rooted include paths are not allowed.", nameof(includePath));
        var includeSegments = include.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (includeSegments.Length == 0 || includeSegments.All(segment => segment is "." or ".."))
            throw new ArgumentException("The include path does not identify a file.", nameof(includePath));

        var stack = Normalize(containingFilePath)
            .Split('/')
            .SkipLast(1)
            .ToList();

        foreach (var segment in includeSegments)
        {
            if (segment == ".")
                continue;

            if (segment == "..")
            {
                if (stack.Count == 0)
                    throw new ArgumentException("The include path escapes the virtual content root.", nameof(includePath));

                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(segment);
        }

        if (stack.Count == 0)
            throw new ArgumentException("The include path does not identify a file.", nameof(includePath));

        return string.Join('/', stack);
    }
}
