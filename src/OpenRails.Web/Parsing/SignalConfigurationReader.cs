using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads the route's native SIGCFG signal types, draw states, aspects, lights,
/// custom function names, normal subtypes, and script-file declarations.
/// Script source execution is deliberately handled by the signal runtime layer.
/// </summary>
public sealed class SignalConfigurationReader
{
    private static readonly HashSet<string> KnownAspects = new(StringComparer.OrdinalIgnoreCase)
    {
        "STOP", "STOP_AND_PROCEED", "RESTRICTING", "APPROACH_1", "APPROACH_2",
        "APPROACH_3", "CLEAR_1", "CLEAR_2", "UNKNOWN"
    };

    public SignalConfigurationDefinition Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(virtualPath, useTree: false);
            var functions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["NORMAL"] = "NORMAL", ["DISTANCE"] = "DISTANCE", ["REPEATER"] = "REPEATER",
                ["SHUNTING"] = "SHUNTING", ["INFO"] = "INFO", ["SPEED"] = "SPEED",
                ["ALERT"] = "ALERT", ["UNKNOWN"] = "UNKNOWN"
            };
            var normalSubtypes = new List<string>();
            var types = new Dictionary<string, SignalTypeDefinition>(StringComparer.OrdinalIgnoreCase);
            var scripts = new List<string>();
            var diagnostics = new List<string>();

            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("ortssignalfunctions", () => ReadCustomFunctions(reader, functions, diagnostics)),
                new STFReader.TokenProcessor("ortsnormalsubtypes", () => ReadNormalSubtypes(reader, normalSubtypes, diagnostics)),
                new STFReader.TokenProcessor("signaltypes", () => ReadSignalTypes(reader, functions, normalSubtypes, types, diagnostics)),
                new STFReader.TokenProcessor("scriptfiles", () => ReadScriptFiles(reader, scripts))
            });

            return new SignalConfigurationDefinition(virtualPath, types, functions, normalSubtypes, scripts, diagnostics);
        }
    }

    private static void ReadCustomFunctions(STFReader reader, IDictionary<string, string> functions, ICollection<string> diagnostics)
    {
        reader.MustMatch("(");
        var count = ReadCount(reader, "ORTSSignalFunctions");
        var seen = 0;
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("ortssignalfunctiontype", () =>
            {
                reader.MustMatch("(");
                if (seen++ >= count)
                {
                    diagnostics.Add("Ignored extra ORTSSignalFunctionType entry.");
                    reader.SkipRestOfBlock();
                    return;
                }
                var name = reader.ReadString().ToUpperInvariant();
                var underlying = reader.ReadString().ToUpperInvariant();
                if (underlying == ")")
                {
                    reader.StepBackOneItem();
                    underlying = "INFO";
                }
                reader.SkipRestOfBlock();
                if (string.IsNullOrWhiteSpace(name) || name.StartsWith("OR_", StringComparison.Ordinal) ||
                    name.StartsWith("ORTS", StringComparison.Ordinal) || functions.ContainsKey(name))
                {
                    diagnostics.Add($"Ignored invalid or duplicate custom signal function '{name}'.");
                    return;
                }
                if (!functions.ContainsKey(underlying))
                {
                    diagnostics.Add($"Custom signal function '{name}' references unknown base function '{underlying}'.");
                    return;
                }
                functions.Add(name, underlying);
            })
        });
        if (seen != count) diagnostics.Add($"ORTSSignalFunctions declared {count} entries but contained {seen}.");
    }

    private static void ReadNormalSubtypes(STFReader reader, IList<string> subtypes, ICollection<string> diagnostics)
    {
        reader.MustMatch("(");
        var count = ReadCount(reader, "ORTSNormalSubtypes");
        var seen = 0;
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("ortsnormalsubtype", () =>
            {
                reader.MustMatch("(");
                var value = reader.ReadString().ToUpperInvariant();
                reader.SkipRestOfBlock();
                seen++;
                if (seen > count) diagnostics.Add($"Ignored extra ORTSNormalSubtype '{value}'.");
                else if (!subtypes.Contains(value, StringComparer.OrdinalIgnoreCase)) subtypes.Add(value);
                else diagnostics.Add($"Ignored duplicate ORTSNormalSubtype '{value}'.");
            })
        });
        if (seen != count) diagnostics.Add($"ORTSNormalSubtypes declared {count} entries but contained {seen}.");
    }

    private static void ReadSignalTypes(STFReader reader, IDictionary<string, string> functions,
        IList<string> normalSubtypes, IDictionary<string, SignalTypeDefinition> types, ICollection<string> diagnostics)
    {
        reader.MustMatch("(");
        var count = ReadCount(reader, "SignalTypes");
        var seen = 0;
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("signaltype", () =>
            {
                if (seen++ >= count)
                {
                    diagnostics.Add("Ignored extra SignalType entry.");
                    reader.SkipRestOfBlock();
                    return;
                }
                var definition = ReadSignalType(reader, functions, normalSubtypes, diagnostics);
                if (!types.TryAdd(definition.Name, definition))
                    diagnostics.Add($"Ignored duplicate SignalType '{definition.Name}'.");
            })
        });
        if (seen != count) diagnostics.Add($"SignalTypes declared {count} entries but contained {seen}.");
    }

    private static SignalTypeDefinition ReadSignalType(STFReader reader, IDictionary<string, string> functions,
        IList<string> normalSubtypes, ICollection<string> diagnostics)
    {
        reader.MustMatch("(");
        var name = reader.ReadString().ToLowerInvariant();
        string script = "", function = "INFO", lightTexture = "", subtype = "";
        var numClearAhead = -2;
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lights = new List<SignalLightDefinition>();
        var drawStates = new Dictionary<string, SignalDrawStateDefinition>(StringComparer.OrdinalIgnoreCase);
        var aspects = new List<SignalAspectDefinition>();
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("ortsscript", () => script = reader.ReadStringBlock("")?.ToLowerInvariant() ?? ""),
            new STFReader.TokenProcessor("signalfntype", () =>
            {
                var requested = reader.ReadStringBlock("INFO")?.ToUpperInvariant() ?? "INFO";
                if (functions.TryGetValue(requested, out var baseFunction)) function = requested;
                else { diagnostics.Add($"SignalType '{name}' uses unknown function '{requested}', defaulting to INFO."); function = "INFO"; }
            }),
            new STFReader.TokenProcessor("signallighttex", () => lightTexture = reader.ReadStringBlock("")?.ToLowerInvariant() ?? ""),
            new STFReader.TokenProcessor("signallights", () => ReadLights(reader, lights, diagnostics, name)),
            new STFReader.TokenProcessor("signaldrawstates", () => ReadDrawStates(reader, drawStates, diagnostics, name)),
            new STFReader.TokenProcessor("signalaspects", () => ReadAspects(reader, aspects, diagnostics, name)),
            new STFReader.TokenProcessor("signalnumclearahead", () => numClearAhead = reader.ReadIntBlock(numClearAhead)),
            new STFReader.TokenProcessor("ortsnormalsubtype", () =>
            {
                var requested = reader.ReadStringBlock("")?.ToUpperInvariant() ?? "";
                if (normalSubtypes.Contains(requested, StringComparer.OrdinalIgnoreCase)) subtype = requested;
                else diagnostics.Add($"SignalType '{name}' references unknown normal subtype '{requested}'.");
            }),
            new STFReader.TokenProcessor("signalflags", () =>
            {
                reader.MustMatch("(");
                while (!reader.EndOfBlock()) flags.Add(reader.ReadString().ToLowerInvariant());
            })
        });
        foreach (var aspect in aspects)
            if (!drawStates.ContainsKey(aspect.DrawState))
                diagnostics.Add($"SignalType '{name}' aspect '{aspect.Aspect}' references missing draw state '{aspect.DrawState}'.");
        foreach (var state in drawStates.Values)
            foreach (var light in state.Lights)
                if (light.LightIndex >= lights.Count)
                    diagnostics.Add($"SignalType '{name}' draw state '{state.Name}' references missing light {light.LightIndex}.");
        return new SignalTypeDefinition(name, script, function, lightTexture, subtype, numClearAhead,
            flags, lights, drawStates, aspects);
    }

    private static void ReadLights(STFReader reader, ICollection<SignalLightDefinition> lights,
        ICollection<string> diagnostics, string signalType)
    {
        reader.MustMatch("(");
        var count = ReadCount(reader, $"SignalLights in {signalType}");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("signallight", () =>
            {
                if (lights.Count >= count) { diagnostics.Add($"Ignored extra SignalLight in '{signalType}'."); reader.SkipRestOfBlock(); return; }
                reader.MustMatch("(");
                var index = reader.ReadUInt(null);
                var name = reader.ReadString().ToLowerInvariant();
                var radius = 0f; var x = 0f; var y = 0f; var z = 0f;
                var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                reader.ParseBlock(new[]
                {
                    new STFReader.TokenProcessor("radius", () => radius = reader.ReadFloatBlock(STFReader.UNITS.None, 0)),
                    new STFReader.TokenProcessor("position", () =>
                    {
                        reader.MustMatch("(");
                        x = reader.ReadFloat(STFReader.UNITS.None, 0);
                        y = reader.ReadFloat(STFReader.UNITS.None, 0);
                        z = reader.ReadFloat(STFReader.UNITS.None, 0);
                        reader.SkipRestOfBlock();
                    }),
                    new STFReader.TokenProcessor("signalflags", () =>
                    {
                        reader.MustMatch("(");
                        while (!reader.EndOfBlock()) flags.Add(reader.ReadString().ToLowerInvariant());
                    })
                });
                if (!float.IsFinite(radius) || radius < 0 || !float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
                    diagnostics.Add($"SignalType '{signalType}' light '{name}' has invalid geometry.");
                lights.Add(new SignalLightDefinition(index, name, radius, x, y, z, flags));
            })
        });
    }

    private static void ReadDrawStates(STFReader reader, IDictionary<string, SignalDrawStateDefinition> states,
        ICollection<string> diagnostics, string signalType)
    {
        reader.MustMatch("(");
        var count = ReadCount(reader, $"SignalDrawStates in {signalType}");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("signaldrawstate", () =>
            {
                if (states.Count >= count) { diagnostics.Add($"Ignored extra SignalDrawState in '{signalType}'."); reader.SkipRestOfBlock(); return; }
                reader.MustMatch("(");
                var index = reader.ReadInt(null);
                var name = reader.ReadString().ToLowerInvariant();
                var lights = new List<SignalDrawLightDefinition>();
                var semaphorePosition = 0f;
                reader.ParseBlock(new[]
                {
                    new STFReader.TokenProcessor("drawlights", () =>
                    {
                        reader.MustMatch("(");
                        var lightCount = ReadCount(reader, $"DrawLights in {signalType}/{name}");
                        reader.ParseBlock(new[]
                        {
                            new STFReader.TokenProcessor("drawlight", () =>
                            {
                                if (lights.Count >= lightCount) { reader.SkipRestOfBlock(); return; }
                                reader.MustMatch("(");
                                var lightIndex = reader.ReadUInt(null);
                                var flashing = false;
                                reader.ParseBlock(new[]
                                {
                                    new STFReader.TokenProcessor("signalflags", () =>
                                    {
                                        reader.MustMatch("(");
                                        while (!reader.EndOfBlock())
                                            if (reader.ReadString().Equals("flashing", StringComparison.OrdinalIgnoreCase)) flashing = true;
                                    })
                                });
                                lights.Add(new SignalDrawLightDefinition(lightIndex, flashing));
                            })
                        });
                    }),
                    new STFReader.TokenProcessor("semaphorepos", () => semaphorePosition = reader.ReadFloatBlock(STFReader.UNITS.None, 0))
                });
                if (!states.TryAdd(name, new SignalDrawStateDefinition(index, name, lights, semaphorePosition)))
                    diagnostics.Add($"Duplicate draw state '{name}' in signal type '{signalType}'.");
            })
        });
    }

    private static void ReadAspects(STFReader reader, ICollection<SignalAspectDefinition> aspects,
        ICollection<string> diagnostics, string signalType)
    {
        reader.MustMatch("(");
        var count = ReadCount(reader, $"SignalAspects in {signalType}");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("signalaspect", () =>
            {
                if (aspects.Count >= count) { reader.SkipRestOfBlock(); return; }
                reader.MustMatch("(");
                var aspect = reader.ReadString().ToUpperInvariant();
                var drawState = reader.ReadString().ToLowerInvariant();
                var speed = -1f; var asap = false; var reset = false; var noReduction = false;
                reader.ParseBlock(new[]
                {
                    new STFReader.TokenProcessor("speedmph", () => speed = reader.ReadFloatBlock(STFReader.UNITS.None, 0) * 0.44704f),
                    new STFReader.TokenProcessor("speedkph", () => speed = reader.ReadFloatBlock(STFReader.UNITS.None, 0) / 3.6f),
                    new STFReader.TokenProcessor("signalflags", () =>
                    {
                        reader.MustMatch("(");
                        while (!reader.EndOfBlock())
                        {
                            var flag = reader.ReadString().ToLowerInvariant();
                            if (flag == "asap") asap = true;
                            else if (flag == "or_speedreset") reset = true;
                            else if (flag == "or_nospeedreduction") noReduction = true;
                            else diagnostics.Add($"Unknown SignalFlags value '{flag}' in '{signalType}'.");
                        }
                    })
                });
                if (!KnownAspects.Contains(aspect))
                {
                    diagnostics.Add($"Unknown aspect '{aspect}' in signal type '{signalType}', retained as UNKNOWN.");
                    aspect = "UNKNOWN";
                }
                if (aspects.Any(existing => existing.Aspect == aspect))
                {
                    diagnostics.Add($"Duplicate aspect '{aspect}' in signal type '{signalType}' ignored.");
                    return;
                }
                aspects.Add(new SignalAspectDefinition(aspect, drawState, speed, asap, reset, noReduction));
            })
        });
    }

    private static void ReadScriptFiles(STFReader reader, ICollection<string> scripts)
    {
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("scriptfile", () => scripts.Add(reader.ReadStringBlock(null) ?? ""))
        });
    }

    private static int ReadCount(STFReader reader, string label)
    {
        var count = reader.ReadInt(null);
        if (count < 0 || count > 100_000)
            throw new InvalidDataException($"{label} declares invalid entry count {count}.");
        return count;
    }
}

public sealed record SignalConfigurationDefinition(
    string SourcePath,
    IReadOnlyDictionary<string, SignalTypeDefinition> SignalTypes,
    IReadOnlyDictionary<string, string> SignalFunctions,
    IReadOnlyList<string> NormalSubtypes,
    IReadOnlyList<string> ScriptFiles,
    IReadOnlyList<string> Diagnostics);

public sealed record SignalTypeDefinition(
    string Name, string Script, string Function, string LightTexture,
    string NormalSubtype, int NumberClearAhead,
    IReadOnlySet<string> Flags, IReadOnlyList<SignalLightDefinition> Lights,
    IReadOnlyDictionary<string, SignalDrawStateDefinition> DrawStates,
    IReadOnlyList<SignalAspectDefinition> Aspects);

public sealed record SignalLightDefinition(uint Index, string Name, float Radius,
    float X, float Y, float Z, IReadOnlySet<string> Flags);
public sealed record SignalDrawLightDefinition(uint LightIndex, bool Flashing);
public sealed record SignalDrawStateDefinition(int Index, string Name,
    IReadOnlyList<SignalDrawLightDefinition> Lights, float SemaphorePosition);
public sealed record SignalAspectDefinition(string Aspect, string DrawState,
    float SpeedMetersPerSecond, bool Asap, bool ResetSpeed, bool NoSpeedReduction);
