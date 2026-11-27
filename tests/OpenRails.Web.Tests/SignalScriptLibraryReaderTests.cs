using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class SignalScriptLibraryReaderTests
{
    [Fact]
    public void LoadsScriptBlocksAndMatchesSignalTypeScriptReferences()
    {
        using var store = CreateStore(
            ("ROUTES/RouteA/Script/Signal/main.sig", """
                // Native signal script source
                SCRIPT MainSignal
                EXTERN FLOAT STATE;
                FLOAT localState;
                IF STATE == 0 {
                    STATE = 1;
                }
                SCRIPT DistantSignal
                FLOAT distanceState;
                STATE = distanceState;
                """));
        var config = new SignalConfigurationDefinition(
            "ROUTES/RouteA/sigcfg.dat",
            new Dictionary<string, SignalTypeDefinition>
            {
                ["main"] = new("main", "mainsignal", "NORMAL", "", "", -2,
                    new HashSet<string>(), Array.Empty<SignalLightDefinition>(),
                    new Dictionary<string, SignalDrawStateDefinition>(), Array.Empty<SignalAspectDefinition>())
            },
            new Dictionary<string, string>(), Array.Empty<string>(), new[] { "main.sig" }, Array.Empty<string>());

        var library = new SignalScriptLibraryReader().Read(store, "ROUTES/RouteA", config);

        Assert.Equal(2, library.Scripts.Count);
        var script = library.Scripts["mainsignal"];
        Assert.Contains("localState", script.LocalFloats);
        Assert.Contains("STATE", script.ExternalFloats);
        Assert.Equal(2, script.StatementCount);
        Assert.Empty(library.Diagnostics);
    }

    [Fact]
    public void ReportsMissingScriptFilesAndUnbalancedScriptBlocks()
    {
        using var store = CreateStore(("ROUTES/RouteA/Script/Signal/broken.sig", """
            SCRIPT Broken
            IF STATE == 0 {
                STATE = 1;
            """));
        var config = new SignalConfigurationDefinition(
            "ROUTES/RouteA/sigcfg.dat",
            new Dictionary<string, SignalTypeDefinition>
            {
                ["broken"] = new("broken", "missing", "NORMAL", "", "", -2,
                    new HashSet<string>(), Array.Empty<SignalLightDefinition>(),
                    new Dictionary<string, SignalDrawStateDefinition>(), Array.Empty<SignalAspectDefinition>())
            },
            new Dictionary<string, string>(), Array.Empty<string>(),
            new[] { "broken.sig", "missing.sig" }, Array.Empty<string>());

        var library = new SignalScriptLibraryReader().Read(store, "ROUTES/RouteA", config);

        Assert.Contains(library.Diagnostics, item => item.Contains("unclosed"));
        Assert.Contains(library.Diagnostics, item => item.Contains("not found"));
        Assert.Contains(library.Diagnostics, item => item.Contains("references script"));
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(file.Content);
            }
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}
