using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Loads and indexes the native SIGSCR files named by SIGCFG. It preserves source
/// statements and declaration inventories and reports structural errors; this is
/// not an evaluator for the Open Rails signalling instruction set.
/// </summary>
public sealed class SignalScriptLibraryReader
{
    public SignalScriptLibrary Read(IVirtualContentStore store, string routeRoot,
        SignalConfigurationDefinition configuration)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(configuration);
        var normalizedRoot = VirtualPath.Normalize(routeRoot).TrimEnd('/');
        var scripts = new Dictionary<string, SignalScriptDefinition>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<string>();
        foreach (var file in configuration.ScriptFiles)
        {
            if (string.IsNullOrWhiteSpace(file))
                continue;
            var candidate = VirtualPath.Normalize($"{normalizedRoot}/Script/Signal/{file.Replace('\\', '/')}");
            var actualPath = store.Paths.FirstOrDefault(path =>
                string.Equals(VirtualPath.Normalize(path), candidate, StringComparison.OrdinalIgnoreCase));
            if (actualPath is null)
            {
                diagnostics.Add($"Declared signal script '{file}' was not found at '{candidate}'.");
                continue;
            }

            using var stream = store.OpenRead(actualPath);
            using var textReader = new StreamReader(stream);
            var parsed = ParseFile(actualPath, textReader);
            foreach (var script in parsed.Scripts)
            {
                if (!scripts.TryAdd(script.Name, script))
                    diagnostics.Add($"Duplicate SCRIPT block '{script.Name}' in '{actualPath}'.");
            }
            diagnostics.AddRange(parsed.Diagnostics);
        }

        foreach (var type in configuration.SignalTypes.Values)
            if (!string.IsNullOrWhiteSpace(type.Script) && !scripts.ContainsKey(type.Script))
                diagnostics.Add($"SignalType '{type.Name}' references script '{type.Script}' which was not loaded.");

        return new SignalScriptLibrary(scripts, diagnostics);
    }

    private static (IReadOnlyList<SignalScriptDefinition> Scripts, IReadOnlyList<string> Diagnostics)
        ParseFile(string path, TextReader reader)
    {
        var scripts = new List<SignalScriptDefinition>();
        var diagnostics = new List<string>();
        string? currentName = null;
        var lines = new List<string>();
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.StartsWith("SCRIPT ", StringComparison.OrdinalIgnoreCase))
            {
                FinishCurrent();
                currentName = trimmed[7..].Trim();
                lines = [];
                if (currentName.Length == 0)
                    diagnostics.Add($"{path}:{lineNumber}: empty SCRIPT name.");
                continue;
            }
            if (trimmed.StartsWith("REM SCRIPT ", StringComparison.OrdinalIgnoreCase))
            {
                FinishCurrent();
                currentName = null;
                lines = [];
                continue;
            }
            if (currentName is not null && trimmed.Length > 0 &&
                !trimmed.StartsWith("//", StringComparison.Ordinal) &&
                !trimmed.StartsWith("/*", StringComparison.Ordinal))
                lines.Add(trimmed);
        }
        FinishCurrent();
        return (Array.AsReadOnly(scripts.ToArray()), Array.AsReadOnly(diagnostics.ToArray()));

        void FinishCurrent()
        {
            if (string.IsNullOrWhiteSpace(currentName))
                return;
            var locals = new List<string>();
            var externals = new List<string>();
            var statementCount = 0;
            var braceDepth = 0;
            var blockDiagnostics = new List<string>();
            for (var index = 0; index < lines.Count; index++)
            {
                var statement = lines[index];
                if (statement.StartsWith("FLOAT ", StringComparison.OrdinalIgnoreCase))
                    locals.Add(DeclarationName(statement));
                else if (statement.StartsWith("EXTERN FLOAT ", StringComparison.OrdinalIgnoreCase))
                    externals.Add(DeclarationName(statement));
                else if (statement != "{" && statement != "}")
                    statementCount++;

                foreach (var character in statement)
                {
                    if (character == '{') braceDepth++;
                    else if (character == '}')
                    {
                        braceDepth--;
                        if (braceDepth < 0)
                        {
                            blockDiagnostics.Add($"'{currentName}' has an unmatched closing brace near source line {index + 1}.");
                            braceDepth = 0;
                        }
                    }
                }
            }
            if (braceDepth != 0)
                blockDiagnostics.Add($"'{currentName}' has {braceDepth} unclosed script block brace(s).");
            diagnostics.AddRange(blockDiagnostics.Select(message => $"{path}: {message}"));
            scripts.Add(new SignalScriptDefinition(currentName,
                Array.AsReadOnly(lines.ToArray()), Array.AsReadOnly(locals.Where(name => name.Length > 0).ToArray()),
                Array.AsReadOnly(externals.Where(name => name.Length > 0).ToArray()), statementCount,
                Array.AsReadOnly(blockDiagnostics.ToArray())));
            currentName = null;
            lines = [];
        }
    }

    private static string DeclarationName(string line)
    {
        var prefix = line.StartsWith("EXTERN FLOAT ", StringComparison.OrdinalIgnoreCase) ? "EXTERN FLOAT " : "FLOAT ";
        var declaration = line[prefix.Length..].Trim();
        var terminator = declaration.IndexOf(';');
        return (terminator >= 0 ? declaration[..terminator] : declaration).Trim();
    }
}

public sealed record SignalScriptDefinition(
    string Name, IReadOnlyList<string> SourceLines, IReadOnlyList<string> LocalFloats,
    IReadOnlyList<string> ExternalFloats, int StatementCount, IReadOnlyList<string> Diagnostics);

public sealed record SignalScriptLibrary(
    IReadOnlyDictionary<string, SignalScriptDefinition> Scripts,
    IReadOnlyList<string> Diagnostics);
